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

        public DispatchService(ChatBox chat, LspdfrApi api, Settings settings, RecordsLedger records)
        {
            _chat = chat;
            _api = api;
            _settings = settings;
            _records = records;
            _pump = new ReplyPump(settings);
            _unit = "2A" + new Random().Next(10, 99);
        }

        public string Unit { get { return _unit; } }

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
                AutoDispatch();
            }
            catch (Exception ex) { Log.Error("dispatch tick", ex); }
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
            if (_callout != null || _pursuit || _pullover || _arresting) { _lastCallAt = Environment.TickCount; return; }
            if (!_api.PlayerAvailable()) { _lastCallAt = Environment.TickCount; return; }

            var waitMs = Math.Max(30, _settings.AutoCalloutSeconds) * 1000;
            if (Environment.TickCount - _lastCallAt < waitMs) return;

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
                    Transmit("Copy " + _unit + ", you are 10-8 and available for calls.");
                    return;

                case "10-7":
                    _lastStatus = "10-7";
                    _chat.Radio(_unit + " is 10-7, out of service.");
                    Transmit("Copy " + _unit + ", 10-7. Dispatch is clear of you.");
                    return;

                case "10-97":
                    _lastStatus = "10-97";
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
            bool asked = _api.RequestBackup(response, unit) != null;

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
            Report(DispatcherIntent.Backup, "10-13, " + spoken + ".");
        }

        private void PerformAction(DispatcherIntent intent)
        {
            switch (intent)
            {
                case DispatcherIntent.Backup:
                    if (_api.RequestBackup("Code3", "LocalUnit") != null) _dispatched = "a backup unit, code 3";
                    break;
                case DispatcherIntent.Ems:
                    if (_api.RequestBackup("Code3", "Ambulance") != null) _dispatched = "an ambulance";
                    break;
                case DispatcherIntent.Fire:
                    if (_api.RequestBackup("Code3", "Firetruck") != null) _dispatched = "the fire department";
                    break;
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
