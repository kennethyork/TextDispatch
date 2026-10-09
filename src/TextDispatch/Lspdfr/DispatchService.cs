using System;
using System.Collections.Generic;
using TextDispatch.Records;
using Rage;
using TextDispatch.Ai;
using TextDispatch.Chat;
using TextDispatch.Dispatch;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// The dispatcher's voice.
    ///
    /// This is the whole point of the plugin: LSPDFR's dispatch talks to you in scanner audio and
    /// on-screen prompts, and this turns the same state into text on the chat channel - and answers
    /// you when you type back.
    ///
    /// Two halves. State is *polled* rather than pushed, because a handful of getters a few times a
    /// second cannot be broken by an API that changed shape between LSPDFR builds. Radio traffic is
    /// *classified* before it is answered, because "send me another unit" has to actually send one -
    /// the model writes the words, this code performs the action, and the answer is only allowed to
    /// claim what really happened.
    /// </summary>
    internal sealed class DispatchService
    {
        private readonly ChatBox _chat;
        private readonly LspdfrApi _api;
        private readonly Settings _settings;
        private readonly ReplyPump _pump;

        private readonly List<string> _radioLog = new List<string>();
        private readonly string _unit;

        /// <summary>Set only when a unit really was sent, so dispatch can say so truthfully.</summary>
        private string _dispatched;

        private object _callout;
        private string _calloutState = "";
        private string _calloutName = "";
        private bool _pursuit;
        private bool _pullover;
        private bool _arresting;
        private bool _onScene;
        private string _lastStatus = "";
        private int _tick;
        private readonly RecordsLedger _records;
        private readonly ShiftLog _shift;

        /// <summary>The call most recently taken, kept after it closes so a report can still name it.</summary>
        private string _lastCallName;
        private string _countedCall;
        private int _onSceneAt;

        /// <summary>When the next follow-up may be said: not in the first moments of a session, and not back to back.</summary>
        private int _nextFollowUpAt = Environment.TickCount + 20000;

        /// <summary>A warrant service in progress - a call raised from the records, not from a callout pack.</summary>
        private WarrantService _warrant;
        private int _lastWarrantAt = Environment.TickCount;

        /// <summary>The rest of the city on the radio.</summary>
        private RadioTraffic _traffic;
        private int _nextTrafficAt = Environment.TickCount + 60000;

        /// <summary>A unit asked for and sent, so /units can say where it is.</summary>
        internal sealed class SentUnit
        {
            public string What;
            public Vehicle Vehicle;
            public int At;
        }

        private readonly List<SentUnit> _units = new List<SentUnit>();

        public DispatchService(ChatBox chat, LspdfrApi api, Settings settings, RecordsLedger records, ShiftLog shift)
        {
            _chat = chat;
            _api = api;
            _settings = settings;
            _records = records;
            _shift = shift;
            _pump = new ReplyPump(settings);

            // The same callsign every session once one has been given - a unit number that changes
            // every time the game starts is not much of an identity.
            if (string.IsNullOrEmpty(records.Unit))
            {
                records.Unit = "2A" + new Random().Next(10, 99);
                records.Dirty = true;
            }
            _unit = records.Unit;
            _traffic = new RadioTraffic(_unit, Environment.TickCount);
        }

        // ------------------------------------------------------------------ units sent

        private void Sent(string what, Vehicle vehicle)
        {
            if (vehicle == null) return;
            _units.Add(new SentUnit { What = what, Vehicle = vehicle, At = Environment.TickCount });
            if (_units.Count > 12) _units.RemoveAt(0);
        }

        /// <summary>The units sent in the last fifteen minutes that are still in the world.</summary>
        public List<SentUnit> Units()
        {
            _units.RemoveAll(u =>
            {
                try { return u.Vehicle == null || !u.Vehicle.Exists() || Environment.TickCount - u.At > 15 * 60 * 1000; }
                catch { return true; }
            });
            return new List<SentUnit>(_units);
        }

        // ------------------------------------------------------------------ warrant service

        public WarrantService Warrant { get { return _warrant != null && _warrant.Active ? _warrant : null; } }

        /// <summary>
        /// Send the player to pick somebody up on a warrant. With no person named, the one on file
        /// longest - anyone the player has met who is wanted now. Returns why not, or null.
        /// </summary>
        public string ServeWarrant(PersonRecord person)
        {
            if (!Plugin.OnDuty) return "you are off duty";
            if (Warrant != null) return "you are already on a warrant service, for " + _warrant.Person.Name;
            if (_callout != null) return "you are on a call";

            if (person == null)
            {
                var wanted = _records.KnownWanted();
                if (wanted.Count == 0) return "nobody you have dealt with has a warrant";
                person = wanted[_random.Next(wanted.Count)];
            }
            if (!person.Wanted) return person.Name + " has no warrant";

            var job = new WarrantService(_api, _records, person);
            if (!job.Start()) return "the game would not put them anywhere - try again in a moment";

            _warrant = job;
            _lastWarrantAt = Environment.TickCount;
            _lastCallAt = Environment.TickCount;
            _lastCallName = job.Name;
            _onScene = false;
            _shift.Call(job.Name);
            _records.Touch(person);

            Transmit(_unit + ", warrant service: " + person.Name + ", " + person.Age + ", wanted for " +
                     (person.WarrantFor ?? "an outstanding warrant") + ". Last known at " + job.Location +
                     ". It is on your map. Advise 10-97.");
            return null;
        }

        public void CancelWarrant()
        {
            if (Warrant == null) return;
            _warrant.Cancel();
            _warrant = null;
            _lastCallAt = Environment.TickCount;
        }

        private void WatchWarrant()
        {
            if (_warrant == null) return;

            bool served;
            var closing = _warrant.Update(out served);
            if (closing == null) return;

            if (served) _shift.Arrest();
            Log.Line("warrant service closed: " + closing);
            Transmit(_unit + ", " + closing + (served ? " Show me 10-8 when you are clear." : ""));
            _warrant = null;
            _onScene = false;
            _lastCallAt = Environment.TickCount;
        }

        // ------------------------------------------------------------------ the rest of the city

        private void OtherUnits()
        {
            if (!_settings.RadioTraffic || _settings.ChatterLevel < 1) return;
            if (Environment.TickCount - _nextTrafficAt < 0) return;

            // Every minute and a half to four minutes, and never over the top of the player's own business.
            _nextTrafficAt = Environment.TickCount + (90 + _random.Next(150)) * 1000;
            if (_pursuit || _pump.Busy) return;

            var line = _traffic.Next();
            _chat.Radio(line);
        }

        // ------------------------------------------------------------------ the stop: who is in the car

        /// <summary>
        /// When a stop starts, run the plate and the driver the way a dispatcher would without being
        /// asked - and say so loudly when something comes back: stolen, a BOLO, a warrant.
        /// </summary>
        private void RunTheStop()
        {
            try
            {
                var vehicle = _api.PulloverVehicle();
                var driver = _api.PulloverSuspect();

                PersonRecord person = null;
                if (driver != null && driver.Exists())
                {
                    var handle = unchecked((int)driver.Handle.Value);
                    person = _records.EnsurePerson(handle, Dialogue.Identities.NameFor(handle));
                    _records.Touch(person);
                }

                VehicleRecord plate = null;
                if (vehicle != null && vehicle.Exists())
                {
                    string number = null, model = null;
                    try { number = vehicle.LicensePlate; } catch { }
                    try { model = vehicle.Model.Name; } catch { }
                    if (!string.IsNullOrWhiteSpace(number)) plate = _records.EnsureVehicle(number, model);
                }

                var flags = new List<string>();
                if (plate != null)
                {
                    if (plate.ReportedStolen) flags.Add(plate.Plate + " is reported STOLEN");
                    if (!plate.Insured) flags.Add("no insurance on " + plate.Plate);
                    foreach (var bolo in _records.Bolos)
                        if (BoloMatches(bolo, plate.Plate)) flags.Add(plate.Plate + " matches a BOLO: " + bolo.Reason);
                }
                if (person != null)
                {
                    if (person.Wanted) flags.Add("your driver, " + person.Name + ", has a WARRANT for " + person.WarrantFor);
                    foreach (var bolo in _records.Bolos)
                        if (BoloMatches(bolo, person.Name)) flags.Add(person.Name + " matches a BOLO: " + bolo.Reason);
                }

                if (flags.Count > 0)
                {
                    Transmit(_unit + ", be advised: " + string.Join("; ", flags.ToArray()) + ".");
                }
                else if (plate != null)
                {
                    Transmit(plate.Plate + " comes back to " + plate.OwnerName + ", " + plate.Model + ", no flags" +
                             (person != null ? ". Driver " + person.Name + " is clear." : "."), 1);
                }
            }
            catch (Exception ex) { Log.Error("running the stop", ex); }
        }

        public string Unit { get { return _unit; } }

        // ------------------------------------------------------------------ what /status and /report read

        /// <summary>The call in progress, or null.</summary>
        public string CurrentCallName
        {
            get
            {
                if (Warrant != null) return _warrant.Name;
                return _callout == null || string.IsNullOrEmpty(_calloutName) ? null : _calloutName;
            }
        }

        public string CurrentCallState
        {
            get
            {
                if (Warrant != null) return _warrant.Fled ? "pursuit" : "running";
                return _callout == null ? null : _calloutState;
            }
        }

        /// <summary>The call in progress, or the last one taken - what a report is written about.</summary>
        public string ReportCallName { get { return CurrentCallName ?? _lastCallName; } }

        public string LastStatus { get { return _lastStatus; } }
        public bool InPursuit { get { return _pursuit; } }
        public bool OnTrafficStop { get { return _pullover; } }

        /// <summary>Seconds on scene, or -1 when not on scene.</summary>
        public int OnSceneSeconds { get { return _onScene && _onSceneAt != 0 ? (Environment.TickCount - _onSceneAt) / 1000 : -1; } }

        /// <summary>Seconds until dispatch raises a call by itself, or -1 when it will not.</summary>
        public int NextAutoCallSeconds
        {
            get
            {
                if (!_settings.AutoCallouts || _callout != null || Warrant != null || _pursuit || _pullover) return -1;
                var waitMs = Math.Max(30, _settings.AutoCalloutSeconds) * 1000;
                return Math.Max(0, (waitMs - (Environment.TickCount - _lastCallAt)) / 1000);
            }
        }

        /// <summary>Something dispatch says in answer to a command, always shown.</summary>
        public void Tell(string line) { Transmit(line); }

        // ------------------------------------------------------------------ the shift

        /// <summary>Start a shift if none is running - going on duty, or 10-8 after a 10-7.</summary>
        public void StartShift()
        {
            if (_shift.Running) return;
            _shift.Start(_unit);
            _records.Dirty = true;
            Log.Line("shift: started");
        }

        /// <summary>
        /// End the shift, read the summary back on the radio, and file it. Said in full whatever the
        /// chatter level: it is the answer to the player saying they are done.
        /// </summary>
        public void EndShift(string why)
        {
            var shift = _shift.End();
            if (shift == null) return;

            _records.AddShift(shift);
            Log.Line("shift: ended (" + why + ") - " + string.Join(" ", ShiftLog.Describe(shift).ToArray()));

            var lines = ShiftLog.Describe(shift);
            Transmit(_unit + ", end of shift. " + (lines.Count > 0 ? lines[0] : ""));
            for (var i = 1; i < lines.Count; i++) Transmit(lines[i]);

            if (_records.FollowUps.Count > 0)
                Transmit(_records.FollowUps.Count + " case(s) still with the courts - you will hear about them next shift.");
        }

        /// <summary>True while the dispatcher is composing a reply.</summary>
        public bool ModelBusy { get { return _pump.Busy; } }

        // ------------------------------------------------------------------ tick

        public void Update()
        {
            // Delivery and model collection have to happen every tick; watching the world does not.
            _pump.Update();

            if (++_tick % 10 != 0) return;

            try
            {
                if (!_api.Available) return;
                WatchCallout();
                WatchPursuit();
                WatchPullover();
                WatchArrest();
                WatchWarrant();
                AutoDispatch();
                FollowUps();
                OtherUnits();
            }
            catch (Exception ex) { Log.Error("dispatch tick", ex); }
        }

        /// <summary>
        /// What came back from the courts. One at a time, ten seconds apart, and not in the middle of a
        /// pursuit - it is news, not an emergency, and it can wait for the player to be listening.
        /// </summary>
        private void FollowUps()
        {
            if (_pursuit || Environment.TickCount - _nextFollowUpAt < 0) return;

            var due = _records.DueFollowUp(DateTime.UtcNow);
            if (due == null) return;

            _nextFollowUpAt = Environment.TickCount + 10000;

            var about = due.Kind == "citation" ? "your citation" : "your arrest";
            var outcome = _records.Resolve(due);
            Log.Line("follow-up: " + outcome);
            Transmit(_unit + ", follow-up on " + about + " of " + due.Name +
                     (string.IsNullOrEmpty(due.Call) ? "" : " from " + due.Call) + ": " + outcome);
        }

        private void WatchCallout()
        {
            var handle = _api.CurrentCallout();

            if (handle == null)
            {
                if (_callout != null)
                {
                    _lastCallAt = Environment.TickCount;
                    _callout = null;
                    _calloutState = "";
                    _calloutName = "";
                    _countedCall = null;
                    _onScene = false;
                    Transmit(_unit + ", that call is closed. You are clear and 10-8.", 1);
                }
                return;
            }

            // The same call is recognised by its name and state, not by the handle: LSPDFR can hand back
            // a different object for the same pending call, and comparing handles announced it again
            // every time that happened.
            var state = _api.AcceptanceState(handle) ?? "";
            var name = _api.CalloutFriendlyName(handle);
            if (string.IsNullOrEmpty(name)) name = _api.CalloutName(handle);
            if (string.IsNullOrEmpty(name)) name = "a call";

            bool sameCall = name == _calloutName;
            bool sameState = state == _calloutState;
            _callout = handle;
            _lastCallAt = Environment.TickCount;
            if (!sameCall) _onScene = false;

            // A call that is still waiting is taken for the player, a moment after it is announced so
            // the announcement is read first.
            if (state == "Pending" && _settings.AutoAccept)
            {
                if (!sameCall || !sameState) { _pendingSince = Environment.TickCount; _autoAccepted = false; }
                else if (!_autoAccepted && Environment.TickCount - _pendingSince > 2500)
                {
                    _autoAccepted = true;
                    _api.AcceptCallout(handle);
                    _chat.Radio("You: " + _unit + ", I'm taking it.");
                }
            }

            if (!(sameCall && sameState))
            {
                _calloutState = state;
                _calloutName = name;

                switch (state)
                {
                    case "Pending":
                        Transmit("All units, we have " + name + ". " + _unit +
                                 ", do you copy? Reply 'accept' or 'decline'.");
                        break;

                    case "Running":
                        Transmit("Copy " + _unit + ", you are assigned " + name +
                                 ". Advise 10-97 when you are on scene.");

                        // Counted once per call: the same call is seen again every time its state is read.
                        if (_countedCall != name) { _countedCall = name; _shift.Call(name); _records.Dirty = true; }
                        _lastCallName = name;
                        break;

                    case "Ended":
                        Transmit(name + " is closed. " + _unit + ", show me 10-98 when you are clear.", 2);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ automatic calls

        private int _lastCallAt = Environment.TickCount;
        private int _pendingSince;
        private bool _autoAccepted;

        /// <summary>A call asked of LSPDFR by name, waiting to see whether it actually started.</summary>
        private string _asked;
        private int _askedAt;

        /// <summary>What the 911 caller said, while its callout is still being found.</summary>
        private string _911Details;

        /// <summary>
        /// Names LSPDFR would not start - another duty's callouts, which the packs list but do not
        /// register. Remembered so the next pick does not waste its turn on them.
        /// </summary>
        private readonly HashSet<string> _notRegistered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Random _random = new Random();

        /// <summary>
        /// Keep the calls coming. LSPDFR's own timer still runs and its calls are welcome; this only
        /// steps in when the patrol is free and nothing has come for AutoCalloutSeconds.
        ///
        /// LSPDFR's StartCallout gives no answer - a name it does not have registered is silently
        /// ignored - so whether a call really started is read back a few seconds later, and a name
        /// that started nothing is set aside and another one tried.
        /// </summary>
        private void AutoDispatch()
        {
            if (_asked != null)
            {
                if (_api.CurrentCallout() != null) { _asked = null; return; }
                if (Environment.TickCount - _askedAt < 5000) return;

                // Nothing came of it: not a callout this duty has. A 911 call goes straight on to the
                // next best fit; an automatic one simply picks again on the next pass.
                _notRegistered.Add(_asked);
                Log.Line("auto callouts: '" + _asked + "' did not start - not registered for this duty; trying another");
                _asked = null;

                if (_911Details != null)
                {
                    var next = _api.MatchCallout(_911Details, _notRegistered) ?? PickRandom();
                    if (next != null) Ask(next, true);
                    else _911Details = null;
                }
                else
                {
                    _lastCallAt = Environment.TickCount - Math.Max(30, _settings.AutoCalloutSeconds) * 1000;
                }
                return;
            }

            _911Details = null;

            if (!_settings.AutoCallouts || !Plugin.OnDuty) return;
            if (_callout != null || Warrant != null || _pursuit || _pullover || _arresting) { _lastCallAt = Environment.TickCount; return; }
            if (!_api.PlayerAvailable()) { _lastCallAt = Environment.TickCount; return; }

            var waitMs = Math.Max(30, _settings.AutoCalloutSeconds) * 1000;
            if (Environment.TickCount - _lastCallAt < waitMs) return;

            // Now and then the call is one of the player's own: somebody they dealt with who has a warrant
            // now. Not more than once in ten minutes, so the records season the shift rather than run it.
            if (_settings.WarrantCalls && Environment.TickCount - _lastWarrantAt > 10 * 60 * 1000 &&
                _records.KnownWanted().Count > 0 && _random.Next(100) < 30)
            {
                var why = ServeWarrant(null);
                if (why == null) return;
                Log.Line("auto callouts: no warrant service - " + why);
            }

            var pick = PickRandom();
            if (pick == null)
            {
                // Everything was tried and nothing started. Start again from the full list later
                // rather than never - a duty change re-registers everything.
                _notRegistered.Clear();
                _lastCallAt = Environment.TickCount;
                return;
            }

            Ask(pick, false);
        }

        private string PickRandom()
        {
            var choices = new List<string>();
            foreach (var label in _api.CalloutLabels())
                if (!_notRegistered.Contains(label)) choices.Add(label);
            return choices.Count == 0 ? null : choices[_random.Next(choices.Count)];
        }

        private void Ask(string label, bool by911)
        {
            _asked = label;
            _askedAt = Environment.TickCount;
            _lastCallAt = Environment.TickCount;
            Log.Line("auto callouts: asking LSPDFR for '" + label + "'" + (by911 ? " (from a 911 call)" : ""));
            _api.StartCallout(label);
        }

        public bool AutoCallouts
        {
            get { return _settings.AutoCallouts; }
            set { _settings.AutoCallouts = value; _lastCallAt = Environment.TickCount; }
        }

        private void WatchPursuit()
        {
            bool running = _api.ActivePursuit() != null;
            if (running == _pursuit) return;
            _pursuit = running;

            if (running) { _shift.Pursuit(); _records.Dirty = true; }

            if (running)
                Transmit("All units, pursuit in progress. " + _unit +
                         ", advise if you need backup - say it on the radio and I will send it.", 1);
            else
                Transmit("Pursuit has ended. " + _unit + ", advise your status.", 2);
        }

        private void WatchPullover()
        {
            bool stopping = _api.PlayerPerformingPullover();
            if (stopping == _pullover) return;
            _pullover = stopping;

            if (stopping) { _shift.Stop(); _records.Dirty = true; }
            if (stopping) RunTheStop();

            if (stopping)
                Transmit(_unit + ", I show you on a traffic stop. Run the plate and tell me what you have.", 1);
            else
                Transmit("Traffic stop cleared. File it before you close the call.", 1);
        }

        private void WatchArrest()
        {
            bool arresting = _api.PlayerArresting();
            if (arresting == _arresting) return;
            _arresting = arresting;

            if (arresting)
                Transmit(_unit + ", one in custody. Ask me for transport when you are ready.", 1);
        }

        // ------------------------------------------------------------------ talking to dispatch

        /// <summary>
        /// Anything the unit transmits that is not a bare status code. This is the AI dispatcher:
        /// say what is actually happening and it answers like somebody on the other end of a radio.
        /// </summary>
        public void Say(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;

            var transcript = Transcript(6);
            Echo(text);

            // A bare code is procedural, and the scripted answer for it is exact - no reason to
            // paraphrase it through a model.
            var code = DispatcherBrain.DetectCode(text);
            if (code != null && text.Length <= 12) { Acknowledge(code); return; }

            // A plate, or a name the records already hold, is a records request - answered from the
            // same ledger the terminal reads. Deterministic on purpose: the reply *is* the record
            // rather than a sentence about one, so it cannot be invented and costs no model time.
            if (TryRecordsRequest(text)) return;

            var intent = DispatcherBrain.Classify(text);

            // The action happens here, deterministically, so that anything the answer claims is
            // true by the time the unit hears it.
            if (DispatcherBrain.NeedsAction(intent)) PerformAction(intent);

            Reply(intent, text, transcript);
        }

        /// <summary>
        /// Dispatch answers something the unit did through a command rather than typed as a sentence.
        /// The action has already happened by the time this is called - this produces only the words.
        /// </summary>
        public void Report(DispatcherIntent intent, string radioLine)
        {
            if (string.IsNullOrEmpty(radioLine)) return;

            var transcript = Transcript(6);
            Echo(radioLine);
            Reply(intent, radioLine, transcript);
        }

        /// <summary>A 911 call the unit is passing to dispatch.</summary>
        public void Incident(string details)
        {
            var line = "911 call received - " +
                       (string.IsNullOrWhiteSpace(details) ? "no details given" : details);

            var transcript = Transcript(6);
            Echo(line);

            // A free unit gets the call itself: the callout whose name best fits what the caller
            // said, or any call for this duty if nothing fits. If none of those turns out to be
            // registered, AutoDispatch moves on to the next fit by itself.
            if (Plugin.OnDuty && _callout == null && _asked == null && !string.IsNullOrWhiteSpace(details))
            {
                var label = _api.MatchCallout(details, _notRegistered) ?? PickRandom();
                if (label != null)
                {
                    _911Details = details;
                    Ask(label, true);
                    Transmit("Copy " + _unit + ", 911 caller reports " + details.Trim() + ". Raising it now - stand by.");
                    return;
                }
            }

            // The details decide the response, not the wrapper: "...man with a gun" gets units sent.
            var intent = DispatcherBrain.Classify(details);
            if (DispatcherBrain.NeedsAction(intent)) PerformAction(intent);

            Reply(intent, line, transcript);
        }

        private void Echo(string radioLine)
        {
            _chat.Radio("You: " + radioLine);
            RememberRadio("You: " + radioLine);
        }

        private void Reply(DispatcherIntent intent, string playerLine, string transcript)
        {
            var context = BuildContext();
            var scripted = DispatcherBrain.Scripted(intent, context, playerLine);
            // Tow counts here as well: dispatch may say a recovery is arranged because the vehicle has
            // actually been taken, which is the same rule backup follows.
            var allowIncoming = DispatcherBrain.UnitsPromised(intent);

            // The prompt is built synchronously from what really happened; the flag is then cleared
            // so it cannot leak into the next transmission.
            string system = null, user = null;
            if (_settings.UseModel) DispatcherBrain.BuildPrompt(context, transcript, playerLine, out system, out user);
            _dispatched = null;

            if (!_settings.UseModel)
            {
                Transmit(scripted);
                return;
            }

            _pump.Ask(
                () =>
                {
                    string error;
                    var reply = LocalModel.Chat(_settings, system, user, out error);
                    if (reply == null)
                    {
                        if (error != null) Log.Line("dispatcher model: " + error);
                        return null;
                    }

                    // The one thing a dispatcher must never invent: responders who were never sent.
                    if (DispatcherBrain.ClaimsUnitsIncoming(reply) && !allowIncoming)
                    {
                        Log.Line("dispatcher model claimed units were coming without a request; used the script");
                        return null;
                    }

                    return reply;
                },
                scripted,
                line => Transmit(line));
        }

        /// <summary>Dispatch answers a status code typed by the player.</summary>
        public void Acknowledge(string code)
        {
            var clock = DateTime.Now.ToString("HH:mm");

            switch (code)
            {
                case "10-8":
                    _lastStatus = "10-8";
                    _chat.Radio(_unit + " is 10-8, in service, " + clock + ".");
                    var newShift = !_shift.Running;
                    StartShift();
                    Transmit("Copy " + _unit + ", you are 10-8 and available for calls." +
                             (newShift ? " New shift started at " + clock + "." : ""));
                    return;

                case "10-7":
                    _lastStatus = "10-7";
                    _chat.Radio(_unit + " is 10-7, out of service.");
                    Transmit("Copy " + _unit + ", 10-7. Dispatch is clear of you.");
                    EndShift("10-7");
                    return;

                case "10-97":
                    _lastStatus = "10-97";
                    if (!_onScene) _onSceneAt = Environment.TickCount;
                    _onScene = true;
                    _chat.Radio(_unit + " is 10-97, on scene.");
                    Transmit("Copy " + _unit + ", 10-97 at " + clock + ". Handle your call.");
                    return;

                case "10-98":
                    _lastStatus = "10-98";
                    _onScene = false;
                    _chat.Radio(_unit + " is 10-98, clear.");
                    Transmit("Copy " + _unit + ", 10-98. You are clear and available.");
                    return;

                case "10-6":
                    _lastStatus = "10-6";
                    _chat.Radio(_unit + " is 10-6, busy.");
                    Transmit("Copy " + _unit + ", 10-6 at " + clock + ".");
                    return;

                case "code-3":
                    _chat.Radio(_unit + " responding code 3.");
                    Transmit("Copy " + _unit + ", code 3. All units be advised.");
                    return;

                case "code-4":
                    _chat.Radio(_unit + " is code 4, no further assistance needed.");
                    Transmit("Copy " + _unit + ", code 4 at " + clock + ".");
                    return;
            }
        }

        /// <summary>Used by /backup - an explicit request, with the spoken line worked out by the caller.</summary>
        public void RequestBackup(string response, string unit, string spoken)
        {
            var vehicle = _api.RequestBackup(response, unit);
            bool asked = vehicle != null;
            Sent(spoken, vehicle);

            if (!asked)
            {
                // Nothing was dispatched, so the answer must not be allowed to say otherwise - and
                // it is not worth asking a model to word a request that did not go through.
                Log.Line("backup: LSPDFR did not accept the request (" + response + "/" + unit + ")");
                Echo("10-13, " + spoken + ".");
                Transmit("Copy " + _unit + ", " + spoken + " requested through the normal channel.");
                return;
            }

            _dispatched = spoken;
            _shift.Backup();
            Report(DispatcherIntent.Backup, "10-13, " + spoken + ".");
        }

        private void PerformAction(DispatcherIntent intent)
        {
            switch (intent)
            {
                case DispatcherIntent.Backup:
                {
                    var sent = _api.RequestBackup("Code3", "LocalUnit");
                    if (sent != null) { _dispatched = "a backup unit, code 3"; _shift.Backup(); Sent("backup, code 3", sent); }
                    break;
                }
                case DispatcherIntent.Ems:
                {
                    var sent = _api.RequestBackup("Code3", "Ambulance");
                    if (sent != null) { _dispatched = "an ambulance"; Sent("an ambulance", sent); }
                    break;
                }
                case DispatcherIntent.Fire:
                {
                    var sent = _api.RequestBackup("Code3", "Firetruck");
                    if (sent != null) { _dispatched = "the fire department"; Sent("the fire department", sent); }
                    break;
                }
                case DispatcherIntent.Transport:
                    var ped = _api.NearestPed(15f);
                    if (ped != null) { _api.RequestTransport(ped); _dispatched = "a transport unit"; }
                    break;
            }
        }

        // ------------------------------------------------------------------ context

        private DispatchContext BuildContext()
        {
            var context = new DispatchContext
            {
                Unit = _unit,
                CalloutRunning = _callout != null,
                CalloutName = _calloutName,
                CalloutState = _calloutState,
                OnScene = _onScene,
                Pursuit = _pursuit,
                Pullover = _pullover,
                LastStatus = _lastStatus
            };

            try
            {
                context.UnitsDispatched = _dispatched;
                context.Available = _api.PlayerAvailable();
                context.PlayerOnDuty = _api.PedIsCop(Game.LocalPlayer.Character) ||
                                       context.CalloutRunning || _pursuit || _pullover;
                context.Zone = _api.ZoneAt(_api.PlayerPosition());
            }
            catch (Exception ex) { Log.Error("dispatch context", ex); }

            return context;
        }

        private void RememberRadio(string line)
        {
            _radioLog.Add(line);
            if (_radioLog.Count > 14) _radioLog.RemoveAt(0);
        }

        private string Transcript(int lines)
        {
            var start = Math.Max(0, _radioLog.Count - lines);
            return string.Join("\n", _radioLog.GetRange(start, _radioLog.Count - start).ToArray());
        }

        // ------------------------------------------------------------------ records over the radio

        /// <summary>
        /// Answer a plate, or a name the records already hold, the way a dispatcher would. Returns
        /// false when the text is not a records request at all, so it goes on to the dispatcher as
        /// ordinary conversation.
        /// </summary>
        private bool TryRecordsRequest(string text)
        {
            var plate = PlateIn(text);
            if (plate != null)
            {
                var vehicle = _records.EnsureVehicle(plate, null);
                if (vehicle == null) return false;

                Transmit(plate + " comes back to " + vehicle.OwnerName + " - " + vehicle.Model +
                         (vehicle.Insured ? ", insured" : ", NOT insured"));

                if (vehicle.ReportedStolen) Transmit(plate + " is flagged as STOLEN.");

                foreach (var bolo in _records.Bolos)
                    if (BoloMatches(bolo, plate)) Transmit(plate + " matches a BOLO: " + bolo.Reason);

                return true;
            }

            var person = PersonIn(text);
            if (person == null) return false;

            Transmit(person.Name + ", " + person.Age + ", " + person.Occupation + " - " +
                     person.Summary() + ".");

            if (person.Wanted && !string.IsNullOrEmpty(person.WarrantFor))
                Transmit("Warrant on file: " + person.WarrantFor);

            return true;
        }

        /// <summary>
        /// A plate hiding in a sentence: a token of four to eight characters with at least one letter
        /// and one digit, which is the shape GTA gives plates and is not the shape of a word. A token
        /// after "plate" or "reg" counts too, because that is how somebody asks for it out loud.
        /// </summary>
        private static string PlateIn(string text)
        {
            var words = text.Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '\'', '"' },
                                   StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < words.Length; i++)
            {
                var word = words[i].Trim();
                if (LooksLikePlate(word)) return word.ToUpperInvariant();
                if (i > 0 && AskedForPlate(words[i - 1]) && word.Length >= 3) return word.ToUpperInvariant();
            }

            return null;
        }

        private static bool AskedForPlate(string word)
        {
            var w = (word ?? "").Trim().ToLowerInvariant();
            return w == "plate" || w == "plates" || w == "reg" || w == "registration" || w == "tag";
        }

        private static bool LooksLikePlate(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;

            var w = word.Trim();
            if (w.Length < 4 || w.Length > 8) return false;

            var letters = 0;
            var digits = 0;

            foreach (var character in w)
            {
                if (char.IsLetter(character)) letters++;
                else if (char.IsDigit(character)) digits++;
                else return false;
            }

            return letters > 0 && digits > 0;
        }

        /// <summary>
        /// A name on the radio - but only one the records already hold, and only when the sentence
        /// asks about a person. Everything else stays conversation, so an ordinary sentence cannot
        /// turn into a records request by accident.
        /// </summary>
        private PersonRecord PersonIn(string text)
        {
            var asked = text.IndexOf("person", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        text.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        text.IndexOf("warrant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        text.IndexOf("record", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!asked) return null;

            foreach (var person in _records.People)
            {
                if (string.IsNullOrEmpty(person.Name)) continue;
                if (text.IndexOf(person.Name, StringComparison.OrdinalIgnoreCase) >= 0) return person;
            }

            return null;
        }

        private static bool BoloMatches(BoloRecord bolo, string subject)
        {
            if (bolo == null || string.IsNullOrEmpty(bolo.Subject)) return false;

            return bolo.Subject.IndexOf(subject, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   subject.IndexOf(bolo.Subject, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Everything dispatch says goes through here, so the log and the channel agree.
        ///
        ///   needed 0  something asked of the player, or an answer to something they said - always shown
        ///   needed 1  routine narration: a call opening, a pursuit starting, a stop clearing
        ///   needed 2  the quietest line there is: narration about something already over
        ///
        /// At `brief` - the default - levels 0 and 1 reach the box and level 2 goes to the log; at
        /// `quiet` only level 0 does; at `full` everything does. The dispatcher narrating all of it
        /// fills the box faster than anyone can read, and the lines worth reading are the ones that
        /// ask the player something.
        ///
        /// A suppressed line is still remembered as radio traffic, so the dispatcher's own idea of
        /// what it has said does not develop holes just because part of it was not displayed.
        /// </summary>
        private void Transmit(string line, int needed = 0)
        {
            if (string.IsNullOrEmpty(line)) return;

            RememberRadio("Dispatch: " + line);

            if (_settings.ChatterLevel >= needed) _chat.Dispatch(line);
            else Log.Line("radio (chatter=" + _settings.Chatter + ", kept to the log): " + line);
        }
    }
}
