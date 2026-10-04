using System;
using System.Collections.Generic;
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

        public DispatchService(ChatBox chat, LspdfrApi api, Settings settings)
        {
            _chat = chat;
            _api = api;
            _settings = settings;
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
                    _callout = null;
                    _calloutState = "";
                    _calloutName = "";
                    _onScene = false;
                    Transmit(_unit + ", that call is closed. You are clear and 10-8.");
                }
                return;
            }

            if (!Equals(handle, _callout))
            {
                _callout = handle;
                _calloutState = "";
                _onScene = false;
            }

            var state = _api.AcceptanceState(handle) ?? "";
            if (state != _calloutState)
            {
                _calloutState = state;

                var name = _api.CalloutFriendlyName(handle);
                if (string.IsNullOrEmpty(name)) name = _api.CalloutName(handle);
                if (string.IsNullOrEmpty(name)) name = "a call";
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
                        Transmit(name + " is closed. " + _unit + ", show me 10-98 when you are clear.");
                        break;
                }
            }
        }

        private void WatchPursuit()
        {
            bool running = _api.ActivePursuit() != null;
            if (running == _pursuit) return;
            _pursuit = running;

            if (running)
                Transmit("All units, pursuit in progress. " + _unit +
                         ", advise if you need backup - say it on the radio and I will send it.");
            else
                Transmit("Pursuit has ended. " + _unit + ", advise your status.");
        }

        private void WatchPullover()
        {
            bool stopping = _api.PlayerPerformingPullover();
            if (stopping == _pullover) return;
            _pullover = stopping;

            if (stopping)
                Transmit(_unit + ", I show you on a traffic stop. Run the plate and tell me what you have.");
            else
                Transmit("Traffic stop cleared. File it before you close the call.");
        }

        private void WatchArrest()
        {
            bool arresting = _api.PlayerArresting();
            if (arresting == _arresting) return;
            _arresting = arresting;

            if (arresting)
                Transmit(_unit + ", one in custody. Ask me for transport when you are ready.");
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
                Transmit);
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

        /// <summary>Everything dispatch says goes through here, so the log and the channel agree.</summary>
        private void Transmit(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _chat.Dispatch(line);
            RememberRadio("Dispatch: " + line);
        }
    }
}
