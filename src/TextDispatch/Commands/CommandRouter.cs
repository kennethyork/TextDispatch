using System;
using System.Globalization;
using Rage;
using TextDispatch.Chat;
using TextDispatch.Dialogue;
using TextDispatch.Dispatch;
using TextDispatch.Lspdfr;

namespace TextDispatch.Commands
{
    /// <summary>
    /// Turns a typed line into something that happens.
    ///
    /// Two grammars share one box, the way a roleplay server does it: plain text is speech, and a
    /// leading slash is a command. Status codes are accepted bare - typing "10-97" is radio traffic,
    /// not a command - because that is how an officer actually talks.
    /// </summary>
    internal sealed class CommandRouter
    {
        private readonly ChatBox _chat;
        private readonly LspdfrApi _api;
        private readonly DispatchService _dispatch;
        private readonly DialogueService _dialogue;

        public CommandRouter(ChatBox chat, LspdfrApi api, DispatchService dispatch, DialogueService dialogue)
        {
            _chat = chat;
            _api = api;
            _dispatch = dispatch;
            _dialogue = dialogue;
        }

        public void Handle(string raw)
        {
            var text = (raw ?? "").Trim();
            if (text.Length == 0) return;

            if (text[0] == '/') { Command(text.Substring(1)); return; }

            var code = AsStatusCode(text);
            if (code != null) { Status(code); return; }

            // Anything else is speech, spoken where you are standing - and the people who can hear
            // it answer, which is the whole point.
            _dialogue.Say(text, SpeechMode.Say);
        }

        /// <summary>
        /// A bare status code is radio traffic, not speech - but only the codes themselves are
        /// treated that way. Ordinary words stay speech, so "clear" still means clear.
        /// </summary>
        private static string AsStatusCode(string text)
        {
            var t = text.ToLowerInvariant().Trim();
            if (t.Length == 0 || t.Length > 12) return null;

            switch (t)
            {
                case "10-8": case "ten eight": return "10-8";
                case "10-7": case "ten seven": return "10-7";
                case "10-6": case "ten six": return "10-6";
                case "10-97": case "ten ninety seven": return "10-97";
                case "10-98": case "ten ninety eight": return "10-98";
                case "10-13": case "ten thirteen": return "10-13";
                case "code 3": case "code3": case "code-3": return "code-3";
                case "code 4": case "code4": case "code-4": return "code-4";
            }

            return null;
        }

        private void Status(string code)
        {
            if (code == "10-13") { Backup(""); return; }

            _chat.Radio("You: " + code.ToUpperInvariant());
            _dispatch.Acknowledge(code);
        }

        private void Command(string body)
        {
            var split = body.IndexOf(' ');
            var name = (split < 0 ? body : body.Substring(0, split)).ToLowerInvariant().Trim();
            var argument = split < 0 ? "" : body.Substring(split + 1).Trim();

            switch (name)
            {
                case "help": Help(); return;

                // ---------------------------------------------------- roleplay
                case "me": _chat.Me("You", argument); return;
                case "do": _chat.Do(argument); return;
                case "b":
                case "ooc": _chat.Write(ChatChannel.Do, "", "(( OOC: " + argument + " ))"); return;
                case "r":
                case "radio": _dispatch.Say(argument); return;

                // ---------------------------------------------------- talking to people
                case "s":
                case "say": _dialogue.Say(argument, SpeechMode.Say); return;
                case "w":
                case "whisper": _dialogue.Say(argument, SpeechMode.Whisper); return;
                case "shout":
                case "y":
                case "yell": _dialogue.Say(argument, SpeechMode.Shout); return;
                case "who": _dialogue.WhoIsAround(); return;
                case "talk": TalkTo(argument); return;
                case "endtalk": _dialogue.StopTalking(); return;

                // ---------------------------------------------------- callouts
                case "accept": Accept(); return;
                case "decline": Decline(); return;
                case "callout":
                case "start": StartCallout(argument); return;
                case "calls": ListCallouts(argument); return;
                case "endcall": EndCall(); return;
                case "available": Available(argument); return;

                // ---------------------------------------------------- on scene
                case "backup": Backup(argument); return;
                case "stop":
                case "pullover": TrafficStop(); return;
                case "endstop": EndStop(); return;
                case "transport": Transport(); return;
                case "cuff": Cuff(); return;
                case "frisk": Frisk(); return;
                case "id": ShowId(); return;
                case "record": ShowRecord(); return;
                case "owner": ShowOwner(); return;
                case "zone": ShowZone(); return;
                case "pursuit": StartPursuit(); return;
                case "endpursuit": EndPursuit(); return;
                case "calledin": CalledIn(); return;
                case "panic": Panic(); return;
                case "911": Call911(argument); return;

                // ---------------------------------------------------- the box itself
                case "ui": SetScale(argument); return;
                case "font": SetFont(argument); return;
                case "fontsize": SetFontSize(argument); return;
                case "lines": SetLines(argument); return;
                case "clear":
                case "cls": _chat.Clear(); return;
            }

            _chat.Error("Unknown command '/" + name + "'. Type /help.");
        }

        private void TalkTo(string argument)
        {
            int index;
            if (!int.TryParse(argument, out index))
            {
                _chat.Error("Usage: /talk <number>  -  see /who for who is nearby.");
                return;
            }
            _dialogue.TalkTo(index);
        }

        // ------------------------------------------------------------------ callouts

        private void Accept()
        {
            if (!_api.Available) { _chat.Error("LSPDFR is not running."); return; }

            var handle = _api.CurrentCallout();
            if (handle == null) { _chat.Error("No call is waiting."); return; }

            var state = _api.AcceptanceState(handle) ?? "";
            if (state == "Running") { _chat.Notice("You are already on that call."); return; }
            if (state != "Pending") { _chat.Notice("That call is not waiting for an answer."); return; }

            _api.AcceptCallout(handle);
            _chat.Radio("You: " + _dispatch.Unit + ", I'm taking it.");
            // Dispatch's own confirmation comes from DispatchService when the state turns Running.
            // Saying it here too made dispatch answer the same thing twice.
        }

        private void Decline()
        {
            if (!_api.Available) { _chat.Error("LSPDFR is not running."); return; }

            _api.SetPlayerAvailable(false);
            _chat.Radio("You: " + _dispatch.Unit + ", I'm 10-6 on that one.");
            _chat.Dispatch("Copy, returned to the queue. Type '/available on' when you want calls again.");
        }

        private void Available(string argument)
        {
            bool on = argument.Length == 0 || argument.Equals("on", StringComparison.OrdinalIgnoreCase);
            if (argument.Equals("off", StringComparison.OrdinalIgnoreCase)) on = false;

            _api.SetPlayerAvailable(on);
            _chat.Dispatch(on
                ? "Copy " + _dispatch.Unit + ", you are available for calls."
                : "Copy " + _dispatch.Unit + ", you are 10-6 and off the call list.");
        }

        private void StartCallout(string argument)
        {
            if (argument.Length == 0)
            {
                _chat.Error("Usage: /callout <name>  -  see /calls for what this install has.");
                return;
            }

            // The name shown by /calls is a label; LSPDFR wants the callout's class name. Resolve
            // whichever the player typed, and fall back to the raw text if it is not in the list.
            var name = _api.ResolveCallout(argument);

            if (!_api.StartCallout(name))
            {
                _chat.Error("Could not start '" + argument + "' - is LSPDFR running?");
                return;
            }

            _chat.Dispatch("Dispatch is raising a " + argument + " call for you. Stand by.");
        }

        /// <summary>
        /// List the callout catalogue, capped and filterable.
        ///
        /// With several packs installed this runs to hundreds of entries, and dumping them all would
        /// scroll the chat box away and bury whatever the player was doing. So it shows a page, says
        /// how many there really are, and writes the whole filtered list to the log - which is where
        /// a few hundred lines belong.
        /// </summary>
        private void ListCallouts(string filter)
        {
            const int page = 20;

            int total;
            var shown = _api.ListCallouts(filter, page, out total);

            if (total == 0)
            {
                _chat.Error(string.IsNullOrEmpty(filter)
                    ? "No callouts found. Is LSPDFR loaded?"
                    : "No callout matches '" + filter + "'. Try /calls on its own.");
                return;
            }

            var packs = _api.CalloutPacks();
            var scope = string.IsNullOrEmpty(filter) ? "" : " matching '" + filter + "'";

            _chat.Notice("Callouts" + scope + ": " + total + "  from " + packs.Count + " pack(s)");
            foreach (var callout in shown) _chat.Notice("  " + callout);

            if (total > shown.Count)
                _chat.Notice("  ... " + (total - shown.Count) + " more - narrow it with '/calls <text>'.");

            _chat.Notice("Start one with '/callout <name>'. The full list is in the log.");

            foreach (var callout in _api.ListCallouts(filter, int.MaxValue, out int _))
                Log.Line("callout: " + callout);
        }

        private void EndCall()
        {
            _api.StopCallout();
            _chat.Dispatch("Copy " + _dispatch.Unit + ", call terminated at your request. 10-8.");
        }

        // ------------------------------------------------------------------ on scene

        private void Backup(string argument)
        {
            var kind = argument.ToLowerInvariant();
            string response, unit, spoken;

            switch (kind)
            {
                case "swat": case "noose":
                    response = "Code3"; unit = "SwatTeam"; spoken = "SWAT"; break;
                case "air": case "helicopter":
                    response = "Code3"; unit = "AirUnit"; spoken = "air support"; break;
                case "state":
                    response = "Code3"; unit = "StateUnit"; spoken = "a state unit"; break;
                case "ems": case "ambulance": case "medic":
                    response = "Code3"; unit = "Ambulance"; spoken = "an ambulance"; break;
                case "fire": case "firetruck":
                    response = "Code3"; unit = "Firetruck"; spoken = "the fire department"; break;
                case "transport":
                    response = "SuspectTransporter"; unit = "PrisonerTransport"; spoken = "a transport unit"; break;
                case "code2": case "2":
                    response = "Code2"; unit = "LocalUnit"; spoken = "a unit, code 2"; break;
                case "pursuit":
                    response = "Pursuit"; unit = "LocalUnit"; spoken = "pursuit backup"; break;
                default:
                    response = "Code3"; unit = "LocalUnit"; spoken = "backup, code 3"; break;
            }

            _dispatch.RequestBackup(response, unit, spoken);
        }

        /// <summary>
        /// Who you are dealing with: the driver LSPDFR is holding at a traffic stop if there is one,
        /// otherwise the nearest person. Without this, commands used mid-stop act on whoever happens
        /// to be closest - often a passer-by rather than the driver you are talking to.
        /// </summary>
        private Ped Subject(float radius)
        {
            var suspect = _api.PulloverSuspect();
            if (suspect != null)
            {
                try { if (suspect.Exists() && suspect.IsAlive) return suspect; }
                catch { }
            }
            return _api.NearestPed(radius);
        }

        /// <summary>The car in play: the one you stopped, or the one you are sitting in.</summary>
        private Vehicle SubjectVehicle()
        {
            return _api.PulloverVehicle() ?? _api.PlayerVehicle();
        }

        /// <summary>
        /// Pull somebody over from the keyboard.
        ///
        /// LSPDFR's own control is still the primary way to do this; what this does is ask the same
        /// API for a stop on the vehicle nearest you. The call has no return value, so the real
        /// confirmation is dispatch announcing the stop a moment later - which is why that path
        /// exists at all.
        /// </summary>
        private void TrafficStop()
        {
            if (_api.PlayerPerformingPullover())
            {
                _chat.Notice("You are already on a traffic stop. '/endstop' clears it.");
                return;
            }

            var vehicle = _api.NearestVehicle(30f);
            if (vehicle == null)
            {
                _chat.Error("No vehicle within 30m to stop.");
                return;
            }

            var plate = "the vehicle";
            try { if (!string.IsNullOrEmpty(vehicle.LicensePlate)) plate = vehicle.LicensePlate; }
            catch { }

            if (!_api.StartPullover(vehicle))
            {
                _chat.Error("LSPDFR would not start a stop on that vehicle. Pull it over in game with LSPDFR's own control.");
                return;
            }

            _chat.Radio("You: 10-38, stopping " + plate + ".");
            _chat.Notice("Dispatch will confirm the stop if LSPDFR took it.");
        }

        private void EndStop()
        {
            if (!_api.PlayerPerformingPullover())
            {
                _chat.Notice("You are not on a traffic stop.");
                return;
            }

            _api.EndPullover();
            _chat.Radio("You: 10-98, stop is clear.");
            _chat.Notice("Stop released. Dispatch will confirm when it sees you clear.");
        }

        private void Transport()
        {
            var ped = Subject(12f);
            if (ped == null) { _chat.Error("Nobody close enough to transport."); return; }

            _api.RequestTransport(ped);
            _dispatch.Report(DispatcherIntent.Transport, "requesting transport for one in custody");
        }

        private void Cuff()
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough to cuff."); return; }

            if (_api.PedArrested(ped))
            {
                _chat.Notice("They are already in custody.");
                return;
            }

            _api.ArrestPed(ped);
            _chat.Me("You", "cuff them and pat them down.");
            _chat.Dispatch("Copy, one detained. Advise transport when you are ready.");
        }

        private void Frisk()
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough to search."); return; }

            bool frisked = _api.PedFrisked(ped);
            bool contraband = _api.PedCarryingContraband(ped);

            _chat.Do(frisked
                ? "They have already been searched by you."
                : "They have not been searched yet - use LSPDFR's search on them.");

            if (frisked && contraband) _chat.Notice("They are carrying contraband.");
            else if (frisked) _chat.Notice("Nothing illegal on them.");
        }

        private void ShowId()
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough to identify."); return; }

            _api.DisplayPedId(ped);
            var persona = _api.PersonaForPed(ped);
            _chat.Do("You check their identification." + (string.IsNullOrEmpty(persona) ? "" : " (" + persona + ")"));
        }

        private void ShowRecord()
        {
            var vehicle = SubjectVehicle();
            if (vehicle == null) { _chat.Error("Nobody is pulled over and you are not in a vehicle."); return; }

            _api.DisplayVehicleRecord(vehicle);
            var owner = _api.VehicleOwner(vehicle);
            _chat.Do("You run the plate.");
            if (!string.IsNullOrEmpty(owner)) _chat.Notice("Registered owner: " + owner);
        }

        private void ShowOwner()
        {
            var vehicle = SubjectVehicle();
            if (vehicle == null) { _chat.Error("Nobody is pulled over and you are not in a vehicle."); return; }

            var owner = _api.VehicleOwner(vehicle);
            _chat.Notice(string.IsNullOrEmpty(owner) ? "No owner on file." : "Registered to " + owner + ".");
        }

        private void ShowZone()
        {
            var position = _api.PlayerPosition();
            var zone = _api.ZoneAt(position);
            if (string.IsNullOrEmpty(zone))
                zone = string.Format(CultureInfo.InvariantCulture, "{0:0}, {1:0}", position.X, position.Y);

            _chat.Notice("Dispatch shows you at " + zone + ".");
        }

        private void StartPursuit()
        {
            var handle = _api.CreatePursuit();
            if (handle == null) { _chat.Error("Could not start a pursuit."); return; }

            _chat.Dispatch("Copy, pursuit started. Call it in when you commit - type '/calledin'.");
        }

        private void EndPursuit()
        {
            var handle = _api.ActivePursuit();
            if (handle == null) { _chat.Error("There is no pursuit running."); return; }

            _api.ForceEndPursuit(handle);
            _chat.Dispatch("Copy, pursuit terminated. Advise your status.");
        }

        private void CalledIn()
        {
            var handle = _api.ActivePursuit();
            if (handle == null) { _chat.Error("There is no pursuit running."); return; }

            _api.MarkPursuitCalledIn(handle);
            _dispatch.RequestBackup("Pursuit", "LocalUnit", "pursuit backup, in progress");
        }

        private void Panic()
        {
            _dispatch.RequestBackup("Code3", "LocalUnit", "officer needs assistance, send everyone");
        }

        /// <summary>
        /// A 911 call passed to dispatch. The details drive the response, so "man with a gun" gets
        /// units sent rather than a canned acknowledgement.
        /// </summary>
        private void Call911(string argument)
        {
            _dispatch.Incident(argument);
        }

        // ------------------------------------------------------------------ the box

        private void SetScale(string argument)
        {
            float value;
            if (!float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value <= 0f)
            {
                _chat.Error("Usage: /ui <scale>  e.g. /ui 0.8");
                return;
            }
            _chat.UiScale = value;
            _chat.Notice("UI scale set to " + value.ToString("0.##", CultureInfo.InvariantCulture) + ".");
        }

        private void SetFont(string argument)
        {
            if (argument.Length == 0) { _chat.Error("Usage: /font <font name>  e.g. /font Consolas"); return; }
            _chat.FontName = argument;
            _chat.Notice("Font set to " + argument + ".");
        }

        private void SetFontSize(string argument)
        {
            float value;
            if (!float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value <= 0f)
            {
                _chat.Error("Usage: /fontsize <size>  e.g. /fontsize 15");
                return;
            }
            _chat.FontSize = value;
            _chat.Notice("Font size set to " + value.ToString("0.##", CultureInfo.InvariantCulture) + ".");
        }

        private void SetLines(string argument)
        {
            int value;
            if (!int.TryParse(argument, out value) || value < 3 || value > 40)
            {
                _chat.Error("Usage: /lines <3-40>  e.g. /lines 12");
                return;
            }
            _chat.VisibleLines = value;
            _chat.Notice("Showing " + value + " lines.");
        }

        private void Help()
        {
            _chat.Notice("TextDispatch - LSPDFR through a chat box.");
            _chat.Notice("  Talk:     type anything to speak aloud - people nearby answer in text");
            _chat.Notice("            /w <text> whisper   /shout <text> yell   /me <action>   /do <text>");
            _chat.Notice("            /who  list who is nearby    /talk <n>  speak to one of them");
            _chat.Notice("  Radio:    /r <text>  or just type a status code below");
            _chat.Notice("  Status:   10-8  10-7  10-97  10-98  10-6  code 3  code 4");
            _chat.Notice("  Calls:    /accept  /decline  /calls [filter]  /callout <name>  /endcall  /available on|off");
            _chat.Notice("  Stops:    /stop  /endstop   (or pull over with LSPDFR and it is picked up)");
            _chat.Notice("            /id  /frisk  /cuff  /record  /owner all target the driver you stopped");
            _chat.Notice("  Scene:    /backup [swat|air|state|ems|fire|transport|code2]  /transport  /cuff  /zone");
            _chat.Notice("  Pursuit:  /pursuit  /calledin  /endpursuit  /panic  /911 <details>");
            _chat.Notice("  Box:      /ui  /font  /fontsize  /lines  /clear");
            _chat.Notice("  Open the box with T, or / to start typing a command.");
        }
    }
}
