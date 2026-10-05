using System;
using System.Collections.Generic;
using System.Globalization;
using Rage;
using TextDispatch.Chat;
using TextDispatch.Dialogue;
using TextDispatch.Dispatch;
using TextDispatch.Jobs;
using TextDispatch.Lspdfr;
using TextDispatch.Records;

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
        private readonly Settings _settings;
        private readonly RecordsLedger _records;

        /// <summary>The one job marker this plugin has on the map, so asking for another does not leave a trail.</summary>
        private static Blip _jobBlip;

        public CommandRouter(ChatBox chat, LspdfrApi api, DispatchService dispatch, DialogueService dialogue,
                             Settings settings, RecordsLedger records)
        {
            _chat = chat;
            _api = api;
            _dispatch = dispatch;
            _dialogue = dialogue;
            _settings = settings;
            _records = records;
        }

        public void Handle(string raw)
        {
            var text = (raw ?? "").Trim();
            if (text.Length == 0) return;

            if (text[0] == '/') { Command(text.Substring(1)); return; }

            var code = AsStatusCode(text);
            if (code != null) { Status(code); return; }

            // The two words dispatch actually asks for work without the slash, because having to
            // remember one is a way of missing a call. They are the only bare words that mean a
            // command: "yes" and "no" stay speech, since those are exactly the words somebody says
            // to a suspect.
            if (CallWaiting())
            {
                if (text.Equals("accept", StringComparison.OrdinalIgnoreCase)) { Accept(); return; }
                if (text.Equals("decline", StringComparison.OrdinalIgnoreCase)) { Decline(); return; }
            }

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
                case "me":
                    _chat.Me("You", argument);
                    _dialogue.Note("You " + argument);
                    return;
                case "do":
                    _chat.Do(argument);
                    _dialogue.Note("(" + argument + ")");
                    return;
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
                // /y is already "yell", so accepting is /yes - and a bare "accept" works too,
                // which is the word dispatch actually says.
                case "accept":
                case "yes": Accept(); return;
                case "decline":
                case "no": Decline(); return;
                case "key": SetKey(argument); return;
                case "chatter":
                case "spam": SetChatter(argument); return;
                // /block is a road block on the bridge, so this is /typing.
                case "typing": SetBlock(argument); return;
                case "callout":
                case "start": StartCallout(argument); return;
                case "calls": ListCallouts(argument); return;

                // DriverJobs V's civilian work. It is a ScriptHookV script with no API to ask, so
                // the list is read out of the file the mod itself loads.
                case "jobs": ListJobs(argument); return;
                case "job": DescribeJob(argument); return;
                case "endcall": EndCall(); return;
                case "available": Available(argument); return;

                // ---------------------------------------------------- on scene
                case "backup": Backup(argument); return;
                case "ems":
                case "ambulance":
                case "medic": Backup("ems"); return;
                case "fire":
                case "firedept":
                case "lsfd": Backup("fire"); return;
                case "stop":
                case "pullover": TrafficStop(); return;
                case "endstop": EndStop(); return;
                case "tow":
                case "impound": Impound(); return;
                case "transport": Transport(); return;
                case "detain": Detain(); return;
                case "release":
                case "uncuff": Release(); return;
                case "cuff": Cuff(); return;
                case "frisk":
                case "search": Frisk(); return;
                case "id":
                case "licence":
                case "license": ShowId(); return;
                case "record": ShowRecord(); return;
                case "owner": ShowOwner(); return;
                case "zone": ShowZone(); return;

                // ---------------------------------------------------- the car
                case "lock": LockVehicle(true); return;
                case "unlock": LockVehicle(false); return;
                case "engine": Engine(argument); return;
                case "trunk": OpenDoor(5, "trunk"); return;
                case "hood": OpenDoor(4, "hood"); return;
                case "doors":
                case "shut": ShutDoors(); return;
                case "repair":
                case "fix":
                case "fixveh": RepairVehicle(); return;
                case "veh":
                case "spawn": SpawnVehicle(argument); return;

                // ---------------------------------------------------- records terminal
                case "mdt":
                case "terminal": RecordsSummary(); return;
                case "person":
                case "name": PersonLookup(argument); return;
                case "plate": PlateLookup(argument); return;
                case "warrant":
                case "wants": WarrantLookup(argument); return;
                case "bolo": Bolo(argument); return;
                case "arrest": Arrest(argument); return;
                case "cite":
                case "ticket": Cite(argument); return;
                case "pursuit": StartPursuit(); return;
                case "endpursuit": EndPursuit(); return;
                case "calledin": CalledIn(); return;
                case "panic": Panic(); return;
                case "911": Call911(argument); return;

                // ---------------------------------------------------- features other plugins own
                case "k9":
                case "dog": BridgeAction("k9"); return;
                case "spikes":
                case "spikestrips":
                case "stingers": BridgeAction("spikes"); return;
                case "roadblock":
                case "block": BridgeAction("roadblock"); return;
                case "pit": BridgeAction("pit"); return;
                case "felony":
                case "felonystop": BridgeAction("felony"); return;
                case "coroner": BridgeAction("coroner"); return;
                case "animal":
                case "animalcontrol": BridgeAction("animal"); return;
                case "group": BridgeAction("group"); return;
                case "dismiss":
                case "standdown": BridgeAction("dismiss"); return;
                case "platecheck":
                case "runplate": BridgeAction("platecheck"); return;
                case "pedcheck":
                case "runped": BridgeAction("pedcheck"); return;
                case "insurance": BridgeCheck("insurance"); return;
                case "reg":
                case "registration": BridgeCheck("registration"); return;
                case "breath":
                case "breathalyzer":
                case "dui": BridgeCheck("breath"); return;
                case "drugs": BridgeCheck("drugs"); return;
                case "bridges":
                case "frameworks": ShowBridges(); return;

                // ---------------------------------------------------- the install
                case "plugins":
                case "plugin": ShowPlugins(); return;

                // ---------------------------------------------------- the box itself
                case "pos":
                case "corner": SetPosition(argument); return;
                case "margin": SetMargin(argument); return;
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
            // With no number, the nearest person - which is nearly always who was meant, and saves
            // running /who first.
            if (string.IsNullOrWhiteSpace(argument)) { _dialogue.TalkTo(1); return; }

            int index;
            if (!int.TryParse(argument, out index))
            {
                _chat.Error("Usage: /talk  (whoever is nearest)  or  /talk <number>  -  see /who.");
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

        /// <summary>
        /// The civilian jobs DriverJobs V has, in the box.
        ///
        /// There is no API to ask: DriverJobs is a ScriptHookV script with no callout registration
        /// to hang a question on. So the list comes from the file the mod itself loads -
        /// scripts\DriverJobsData\Missions\Jobs.xml - which is why what is shown is what the mod
        /// actually has, including a job edited or added by hand. Not having the mod is a normal
        /// state: then this says so, once, and nothing else changes.
        /// </summary>
        private void ListJobs(string argument)
        {
            var jobs = CivilianJobs.All();
            if (jobs.Count == 0)
            {
                _chat.Error("No civilian jobs to list: " + CivilianJobs.Problem + ".");
                _chat.Notice("They come with DriverJobs V - scripts\\DriverJobsData\\Missions\\Jobs.xml.");
                return;
            }

            var filter = (argument ?? "").Trim();
            var matching = new List<CivilianJob>();
            foreach (var job in jobs)
                if (filter.Length == 0 || job.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) matching.Add(job);

            if (matching.Count == 0)
            {
                _chat.Error("No job matches '" + filter + "'.");
                return;
            }

            const int page = 20;
            var scope = filter.Length == 0 ? "" : " matching '" + filter + "'";
            _chat.Notice("Civilian jobs" + scope + ": " + matching.Count + "  (DriverJobs V)");

            for (int i = 0; i < matching.Count && i < page; i++)
                _chat.Notice("  " + (i + 1) + ". " + matching[i].Name + "  -  " + matching[i].PayText());

            if (matching.Count > page)
                _chat.Notice("  ... " + (matching.Count - page) + " more - narrow it with '/jobs <text>'.");

            _chat.Notice("Mark one with '/job <name or number>'. The whole list is in the log.");

            foreach (var job in matching) Log.Line("job: " + job.LogLine());
        }

        /// <summary>
        /// One job: what the work is, what it pays, what you drive and where it starts - with the
        /// start marked on the map. It cannot take the job for you, because the mod owns that: a job
        /// is started by being at its place, or from the mod's own menu.
        /// </summary>
        private void DescribeJob(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                _chat.Notice("Usage: /job <name or number>  -  /jobs lists them.");
                return;
            }

            string problem;
            var job = CivilianJobs.Find(argument, out problem);
            if (job == null)
            {
                _chat.Error("No single job matches '" + argument.Trim() + "'" +
                            (problem == null ? "" : ": " + problem) + ".");
                _chat.Notice("Try /jobs to see the list.");
                return;
            }

            _chat.Notice(job.Name + "  -  " + job.KindText());

            for (int i = 0; i < job.Lines.Length && i < 4; i++)
                if (job.Lines[i].Length > 0) _chat.Notice("  " + job.Lines[i]);
            if (job.Lines.Length > 4) _chat.Notice("  ...");

            _chat.Notice("  " + job.PayText() + ".  You drive " + job.VehicleText() + ".");
            if (job.HasUniform) _chat.Notice("  This one still puts you in its own work clothes.");

            if (job.HasStart) MarkJob(job);
            else _chat.Notice("  No start point is given in the file - take it from the job menu (Shift + J).");
        }

        /// <summary>
        /// Put a job's start on the map, replacing the last one this plugin marked.
        /// </summary>
        private void MarkJob(CivilianJob job)
        {
            try
            {
                if (_jobBlip != null)
                {
                    try { _jobBlip.Delete(); } catch { }
                    _jobBlip = null;
                }

                _jobBlip = new Blip(new Vector3(job.X, job.Y, job.Z));
                _jobBlip.Name = job.Name;
                _jobBlip.Color = System.Drawing.Color.Yellow;
                _jobBlip.IsRouteEnabled = true;

                // The route as well as the blip: the marker says where, this says how to get there.
                try { Rage.Native.NativeFunction.Natives.SetNewWaypoint(job.X, job.Y); }
                catch (Exception ex) { Log.Error("job waypoint", ex); }

                _chat.Notice("  Marked on the map. Drive there to take it" + (job.Remote
                    ? ", or open the job menu (Shift + J) anywhere in a suitable vehicle."
                    : "."));
            }
            catch (Exception ex)
            {
                Log.Error("job blip", ex);
                _chat.Notice("  Its start is at " +
                             string.Format(CultureInfo.InvariantCulture, "{0:0.#}, {1:0.#}", job.X, job.Y) + ".");
            }
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

            // Other plugins own units LSPDFR has never had - a K9, spike strips, a road block, air
            // support - so if one is installed it answers, and LSPDFR stays the fallback rather than
            // the only option. Which one answered is said out loud, because the player asked for a
            // unit rather than for a mod.
            if (kind.Length > 0 && Plugin.Bridge != null)
            {
                Plugin.Bridge.Ensure();

                var answered = Plugin.Bridge.Backup(kind);
                if (answered != null)
                {
                    _chat.Notice(answered + " is sending " + Spoken(kind) + ".");
                    Log.Line("bridge: backup " + kind + " -> " + answered);
                    return;
                }
            }

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

        /// <summary>
        /// Have the vehicle taken away.
        ///
        /// LSPDFR has no towing system of its own, and there is no tow plugin here to borrow one from,
        /// so this does what a tow ends with: the vehicle leaves the world. Text alone would be a
        /// service that only pretended to happen.
        /// </summary>
        private void Impound()
        {
            var player = Game.LocalPlayer.Character;

            // The one you stopped, or the nearest that is not yours - never your own car.
            var vehicle = _api.PulloverVehicle();
            if (vehicle == null) vehicle = _api.NearestVehicle(20f);

            if (vehicle == null) { _chat.Error("No vehicle close enough to tow."); return; }

            if (player != null && ReferenceEquals(vehicle, player.CurrentVehicle))
            {
                _chat.Error("That is your own vehicle.");
                return;
            }

            var plate = "the vehicle";
            string model = null;
            try { if (!string.IsNullOrEmpty(vehicle.LicensePlate)) plate = vehicle.LicensePlate; }
            catch { }
            try { model = vehicle.Model.Name; }
            catch { }

            try { vehicle.Delete(); }
            catch (Exception ex)
            {
                Log.Error("tow", ex);
                _chat.Error("The tow could not be arranged.");
                return;
            }

            Log.Line("tow: removed " + plate + " (" + (model ?? "unknown model") + ")");

            _chat.Radio("You: " + _dispatch.Unit + ", I need a tow for " + plate + ".");
            _dispatch.Report(DispatcherIntent.Tow,
                "requesting a tow for " + plate + (string.IsNullOrEmpty(model) ? "" : " (" + model + ")"));
        }

        /// <summary>Stop them where they are - the state LSPDFR itself uses for somebody detained.</summary>
        private void Detain()
        {
            var ped = Subject(8f);
            if (ped == null) { _chat.Error("Nobody close enough to detain."); return; }

            _api.StopPed(ped);
            _chat.Me("You", "stop them and hold them there.");
            _dispatch.Report(DispatcherIntent.Report, "one detained and held here");
        }

        /// <summary>Take the cuffs off and let them go.</summary>
        private void Release()
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough to release."); return; }

            bool cleared = _api.ReleasePed(ped);

            // Whether or not LSPDFR gives up the arrested state, the cuffed task has to come off.
            string detail;
            PedActions.Perform(_api, ped, ComplyAction.StandDown, out detail);

            if (!cleared) Log.Line("release: LSPDFR did not clear the arrested state; cleared their tasks instead");

            _chat.Me("You", "take the cuffs off.");
            _dispatch.Report(DispatcherIntent.Report, "releasing them - they are free to go");
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

        // ------------------------------------------------------------------ the car

        /// <summary>The vehicle you are sitting in, for things you do to your own car.</summary>
        private Vehicle MyVehicle()
        {
            try
            {
                var player = Game.LocalPlayer.Character;
                return player == null ? null : player.CurrentVehicle;
            }
            catch { return null; }
        }

        private void LockVehicle(bool locked)
        {
            var vehicle = SubjectVehicle() ?? MyVehicle();
            if (vehicle == null) { _chat.Error("No vehicle."); return; }

            string detail;
            if (!VehicleActions.SetLocked(vehicle, locked, out detail)) { _chat.Error("Could not change the locks."); return; }

            _chat.Me("You", locked ? "lock the car." : "unlock the car.");
            Log.Line("vehicle: " + detail);
        }

        private void Engine(string argument)
        {
            var vehicle = MyVehicle();
            if (vehicle == null) { _chat.Error("You are not in a vehicle."); return; }

            bool on = !argument.Equals("off", StringComparison.OrdinalIgnoreCase);

            string detail;
            if (!VehicleActions.SetEngine(vehicle, on, out detail)) { _chat.Error("Could not change the engine."); return; }

            _chat.Me("You", on ? "start the engine." : "shut the engine off.");
            Log.Line("vehicle: " + detail);
        }

        private void OpenDoor(int index, string what)
        {
            var vehicle = SubjectVehicle();
            if (vehicle == null) { _chat.Error("No vehicle in front of you."); return; }

            string detail;
            if (!VehicleActions.OpenDoor(vehicle, index, out detail)) { _chat.Error("Could not open the " + what + "."); return; }

            _chat.Me("You", "open the " + what + ".");
            Log.Line("vehicle: " + detail);
        }

        private void ShutDoors()
        {
            var vehicle = SubjectVehicle();
            if (vehicle == null) { _chat.Error("No vehicle in front of you."); return; }

            string detail;
            if (!VehicleActions.CloseDoors(vehicle, out detail)) { _chat.Error("Could not shut the doors."); return; }

            _chat.Me("You", "shut the doors.");
            Log.Line("vehicle: " + detail);
        }

        private void RepairVehicle()
        {
            var vehicle = MyVehicle();
            if (vehicle == null) { _chat.Error("You are not in a vehicle."); return; }

            string detail;
            if (!VehicleActions.Repair(vehicle, out detail)) { _chat.Error("Nothing to repair."); return; }

            _chat.Me("You", "give it a quick once-over.");
            Log.Line("vehicle: " + detail);
        }

        private void SpawnVehicle(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                _chat.Error("Usage: /veh <model>   e.g. /veh police2, /veh riot, /veh ambulance");
                return;
            }

            Vehicle spawned;
            string detail;
            if (!VehicleActions.Spawn(argument, out spawned, out detail))
            {
                _chat.Error(detail ?? "Could not bring that vehicle.");
                return;
            }

            _chat.Notice("Brought you a " + argument + ".");
            Log.Line("vehicle: " + detail);
        }

        // ------------------------------------------------------------------ records terminal

        /// <summary>
        /// The record of whoever you are dealing with, created on first meeting.
        ///
        /// The name comes from the same function the chat box uses, so the person the terminal describes
        /// is the person you have been talking to.
        /// </summary>
        private PersonRecord PersonInFront()
        {
            var ped = Subject(6f);
            if (ped == null) return null;

            var handle = unchecked((int)ped.Handle.Value);
            return _records.EnsurePerson(handle, Identities.NameFor(handle));
        }

        private void RecordsSummary()
        {
            _chat.Notice("On file: " + _records.People.Count + " people, " + _records.Vehicles.Count +
                         " vehicles, " + _records.Bolos.Count + " BOLOs");
            _chat.Notice("  you: " + _records.CitationCount + " citations, " + _records.ArrestCount +
                         " arrests, $" + _records.FinesIssued.ToString("0.00") + " in fines");
            _chat.Notice("  /person [name]   /plate [plate]   /warrant [name]   /bolo   /arrest   /cite");
        }

        private void PersonLookup(string argument)
        {
            PersonRecord person;
            if (string.IsNullOrWhiteSpace(argument))
            {
                person = PersonInFront();
                if (person == null) { _chat.Error("Nobody close enough, and no name given."); return; }
            }
            else
            {
                person = _records.FindPerson(argument);
                if (person == null) { _chat.Error("No record for '" + argument + "'."); return; }
            }

            _chat.Notice(person.Name + " - " + person.Age + " - " + person.Occupation + " - " + person.HomeZone);
            _chat.Notice("  " + person.Summary());

            for (int i = 0; i < person.Priors.Count; i++) _chat.Notice("    - prior: " + person.Priors[i]);
            if (person.Wanted) _chat.Notice("  WARRANT: " + person.WarrantFor);
            if (person.UnpaidFines > 0) _chat.Notice("  outstanding fines: $" + person.UnpaidFines.ToString("0.00"));

            foreach (var vehicle in _records.Vehicles)
            {
                if (!string.Equals(vehicle.OwnerName, person.Name, StringComparison.OrdinalIgnoreCase)) continue;
                _chat.Notice("  vehicle: " + vehicle.Plate + " - " + vehicle.Model +
                             (vehicle.ReportedStolen ? "  (reported stolen)" : ""));
            }
        }

        private void PlateLookup(string argument)
        {
            var plate = argument;
            string model = null;

            if (string.IsNullOrWhiteSpace(plate))
            {
                var vehicle = SubjectVehicle();
                if (vehicle == null) { _chat.Error("No vehicle in front of you, and no plate given."); return; }

                try { plate = vehicle.LicensePlate; model = vehicle.Model.Name; }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(plate)) { _chat.Error("Could not read a plate."); return; }

            var record = _records.EnsureVehicle(plate, model);
            if (record == null) { _chat.Error("Could not read a plate."); return; }

            _chat.Notice(record.Plate + " - " + record.Model + " - registered to " + record.OwnerName);
            if (!record.Insured) _chat.Notice("  FLAG: no insurance on file");
            if (record.ReportedStolen) _chat.Notice("  FLAG: reported stolen");

            var owner = _records.FindPerson(record.OwnerName);
            if (owner != null && owner.Wanted)
                _chat.Notice("  FLAG: registered owner has an outstanding warrant");
        }

        private void WarrantLookup(string argument)
        {
            var person = string.IsNullOrWhiteSpace(argument) ? PersonInFront() : _records.FindPerson(argument);
            if (person == null)
            {
                _chat.Error(string.IsNullOrWhiteSpace(argument)
                    ? "Nobody close enough, and no name given."
                    : "No record for '" + argument + "'.");
                return;
            }

            if (!person.Wanted)
            {
                _chat.Notice(person.Name + " - no outstanding warrants.");
                return;
            }

            _chat.Notice("WARRANT - " + person.Name + " - " + person.WarrantFor);
            if (person.Priors.Count > 0) _chat.Notice("  with " + person.Priors.Count + " prior conviction(s) on file");
        }

        private void Bolo(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument) || argument.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                if (_records.Bolos.Count == 0) { _chat.Notice("No active BOLOs."); return; }
                for (int i = 0; i < _records.Bolos.Count; i++)
                    _chat.Notice("BOLO - " + _records.Bolos[i].Subject + " - " + _records.Bolos[i].Reason);
                return;
            }

            if (argument.StartsWith("clear ", StringComparison.OrdinalIgnoreCase))
            {
                var subject = argument.Substring(6).Trim();
                _chat.Notice(_records.ClearBolo(subject)
                    ? "BOLO cleared for " + subject + "."
                    : "No BOLO on file for '" + subject + "'.");
                return;
            }

            if (argument.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = argument.Substring(4).Trim();
                var split = rest.IndexOf(' ');
                if (split <= 0) { _chat.Error("Usage: /bolo add <plate or name> <reason>"); return; }

                var subject = rest.Substring(0, split);
                var reason = rest.Substring(split + 1).Trim();

                _records.AddBolo(subject, reason);
                _chat.Notice("BOLO entered for " + subject + ": " + reason);
                return;
            }

            _chat.Error("Usage: /bolo list  |  /bolo add <plate or name> <reason>  |  /bolo clear <subject>");
        }

        private void Arrest(string argument)
        {
            var inFront = string.IsNullOrWhiteSpace(argument);
            var person = inFront ? PersonInFront() : _records.FindPerson(argument);

            if (person == null)
            {
                _chat.Error(inFront ? "Nobody close enough, and no name given." : "No record for '" + argument + "'.");
                return;
            }

            var offence = person.Wanted && !string.IsNullOrEmpty(person.WarrantFor) ? person.WarrantFor : "booking";
            _chat.Notice(_records.RecordArrest(person, offence));

            // If it is the person standing in front of you, put the cuffs on as well.
            if (inFront)
            {
                var ped = Subject(6f);
                if (ped != null) _api.ArrestPed(ped);
            }

            _dispatch.Report(DispatcherIntent.Report, "one in custody and booked");
        }

        private void Cite(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                _chat.Error("Usage: /cite <name> <offence>   e.g. /cite Reyes speeding");
                return;
            }

            var split = argument.IndexOf(' ');
            var name = split < 0 ? argument : argument.Substring(0, split);
            var offence = split < 0 ? "traffic offence" : argument.Substring(split + 1).Trim();

            var person = _records.FindPerson(name);
            if (person == null) { _chat.Error("No record for '" + name + "'."); return; }

            _chat.Notice(_records.IssueCitation(person, offence, 250.0));
            _dispatch.Report(DispatcherIntent.Report, "citation written for " + offence);
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

        /// <summary>
        /// Move the box to another corner, and remember it. The top-left of an LSPDFR screen is the
        /// busiest patch there is, so this is the setting most likely to need changing.
        /// </summary>
        private void SetPosition(string argument)
        {
            ChatCorner corner;
            if (!ChatCorners.TryParse(argument, out corner))
            {
                _chat.Error("Usage: /pos top-right | top-left | bottom-right | bottom-left" +
                            "   (currently " + ChatCorners.Describe(_chat.Position) + ")");
                return;
            }

            _chat.Position = corner;
            _chat.Notice("Chat box moved to the " + ChatCorners.Describe(corner) + " corner.");

            if (_settings != null)
            {
                _settings.ChatPosition = ChatCorners.Describe(corner);
                _settings.Save();
                _chat.Notice("Saved to TextDispatch.ini.");
            }
        }

        /// <summary>How far the box sits from the screen edge.</summary>
        private void SetMargin(string argument)
        {
            float value;
            if (!float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                value < 0f || value > 400f)
            {
                _chat.Error("Usage: /margin <0-400>   e.g. /margin 40");
                return;
            }

            _chat.Margin = value;
            _chat.Notice("Margin set to " + value.ToString("0") + " pixels.");

            if (_settings != null)
            {
                _settings.ChatMargin = value;
                _settings.Save();
            }
        }

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

        // ------------------------------------------------------------------ the install

        /// <summary>
        /// What LSPDFR actually loaded, and what it did not.
        ///
        /// This gets its own command because the failure it describes is invisible: a plugin one
        /// folder away from where LSPDFR looks, or one with a dependency it does not have, changes
        /// how the game loads not at all. The plugin is simply not there, and nothing says so. The
        /// same report is written to the log, where it survives the game being closed.
        /// </summary>
        private void ShowPlugins()
        {
            var inventory = PluginInventory.Scan(_api);
            inventory.WriteToLog();

            foreach (var line in inventory.Detail())
            {
                _chat.Notice(line);
                Log.Line("plugins: " + line);
            }

            foreach (var warning in inventory.Warnings()) _chat.Error(warning);
        }

        /// <summary>
        /// How much of the dispatcher's traffic reaches the box.
        ///
        /// In game rather than only in the ini, because the right answer changes with what you are
        /// doing: quiet while you are reading your way through a callout, full while you are waiting
        /// for something to happen.
        /// </summary>
        private void SetChatter(string argument)
        {
            var level = (argument ?? "").Trim().ToLowerInvariant();
            if (level != "quiet" && level != "brief" && level != "full")
            {
                _chat.Notice("Chatter is " + _settings.Chatter + ".");
                _chat.Notice("Usage: /chatter quiet | brief | full");
                _chat.Notice("  quiet  only what asks you something   brief  the callouts too (default)   full  everything");
                return;
            }

            _settings.Chatter = level;
            _settings.Save();
            _chat.Notice("Chatter is now " + level + ", and that is saved to the ini.");
        }

        /// <summary>
        /// Whether the keystrokes that spell a sentence are hidden from the other plugins.
        ///
        /// Here rather than only in the ini because the answer is only visible in game, and because
        /// part of it cannot be decided in advance: whether a plugin reads the game's key messages or
        /// the keyboard itself is that plugin's business, not something this one can be told. So
        /// /typing reports which of the two this install turned out to be, and either can be changed
        /// from here. It is not called /block because that is already a road block.
        /// </summary>
        private void SetBlock(string argument)
        {
            var capture = Plugin.Keys;
            if (capture == null)
            {
                _chat.Error("The keyboard is not being watched yet - the box has to be running first.");
                return;
            }

            var parts = (argument ?? "").Trim().ToLowerInvariant()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var what = parts.Length > 0 ? parts[0] : "";

            if (what == "hardware")
            {
                if (parts.Length < 2)
                {
                    _chat.Notice("Hidden keys are " + (capture.Hardware ? "also" : "not") +
                                 " released in the hardware state.");
                    _chat.Notice("Usage: /typing hardware on | off");
                    return;
                }

                if (parts[1] != "on" && parts[1] != "off")
                {
                    _chat.Error("Say which: /typing hardware on | off");
                    return;
                }

                var hardware = parts[1] == "on";
                capture.SetHardware(hardware);
                _settings.HideHardwareKeys = hardware;
                _settings.Save();

                _chat.Notice(hardware
                    ? "Hidden keys now release in the hardware state too. That is what stops a plugin that reads the keyboard directly, and it is saved to the ini."
                    : "Hidden keys are no longer released in the hardware state - a plugin that reads the keyboard directly will see them again. Saved to the ini.");
                return;
            }

            if (what == "on" || what == "off")
            {
                var hide = what == "on";
                capture.SetEnabled(hide);
                _settings.BlockOtherModsKeys = hide;
                _settings.Save();

                _chat.Notice(hide
                    ? "Typing is hidden from the other plugins again, and that is saved to the ini."
                    : "Typing is no longer hidden: the other plugins see it, as they did before. Saved to the ini.");
                return;
            }

            _chat.Notice(capture.Describe());
            if (!string.IsNullOrEmpty(capture.Probe)) _chat.Notice(capture.Probe + ".");
            _chat.Notice("Usage: /typing on | off      /typing hardware on | off");
        }

        /// <summary>True when a call is on the radio waiting for an answer.</summary>
        private bool CallWaiting()
        {
            try
            {
                var handle = _api.CurrentCallout();
                return handle != null && (_api.AcceptanceState(handle) ?? "") == "Pending";
            }
            catch { return false; }
        }

        /// <summary>
        /// Which key, or keys, open the box - changeable while playing, because the key that clashes
        /// is discovered by pressing it and watching something else come up, not by reading an ini.
        /// </summary>
        private void SetKey(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                _chat.Notice("The box opens with " + Plugin.OpenKeyDescription + ".");
                _chat.Notice("Usage: /key Left   -   or several: /key Left,F6   -   or OpenKey in the ini.");
                return;
            }

            string description;
            var problem = Plugin.ChangeOpenKey(argument, out description);
            if (problem != null) { _chat.Error("That key will not work: " + problem + "."); return; }

            _chat.Notice("The box now opens with " + description + ", and that is saved to the ini.");
        }

        /// <summary>How a backup kind reads back to the player.</summary>
        private static string Spoken(string kind)
        {
            switch (kind)
            {
                case "k9": case "dog": case "statek9": return "a K9 unit";
                case "spikes": case "spikestrips": return "spike strips";
                case "roadblock": case "block": return "a road block";
                case "felony": return "a felony stop";
                case "group": return "a group of units";
                case "female": return "a female unit";
                case "pursuit": return "pursuit backup";
                case "traffic": case "stop": return "a traffic stop unit";
                case "coroner": return "the coroner";
                case "animal": return "animal control";
                case "air": case "helicopter": return "air support";
                case "nooseair": return "NOOSE air support";
                case "swat": case "noose": case "localswat": return "SWAT";
                case "state": return "a state unit";
                case "ems": case "ambulance": case "medic": return "an ambulance";
                case "fire": case "firetruck": return "the fire department";
                case "tow": return "a tow truck";
                case "transport": return "a transport unit";
                default: return "backup";
            }
        }

        /// <summary>
        /// Something only another plugin can do. Which one answers is reported, because the player
        /// asked for a thing - a K9, spike strips - rather than for a particular mod.
        /// </summary>
        private void BridgeAction(string what)
        {
            var bridge = Plugin.Bridge;
            if (bridge == null) { _chat.Error("No framework bridge is running."); return; }

            bridge.Ensure();

            string who, said;
            switch (what)
            {
                case "k9": who = bridge.Backup("k9"); said = "a K9 unit"; break;
                case "spikes": who = bridge.Backup("spikes"); said = "spike strips ahead"; break;
                case "roadblock": who = bridge.Backup("roadblock"); said = "a road block"; break;
                case "group": who = bridge.Backup("group"); said = "a group of units"; break;
                case "coroner": who = bridge.Backup("coroner"); said = "the coroner"; break;
                case "animal": who = bridge.Backup("animal"); said = "animal control"; break;
                case "pit": who = bridge.Pit(); said = "a PIT"; break;
                case "felony": who = bridge.FelonyStop(); said = "a felony stop"; break;
                case "dismiss": who = bridge.DismissAll(); said = "standing all units down"; break;
                case "platecheck": who = bridge.RadioPlateCheck(); said = "a plate check on the radio"; break;
                case "pedcheck": who = bridge.RadioPedCheck(); said = "a ped check on the radio"; break;
                default: who = null; said = what; break;
            }

            if (who == null)
            {
                _chat.Error("Nothing installed can do that. /bridges says what can.");
                Log.Line("bridge: '" + what + "' could not be done - " + bridge.Describe());
                return;
            }

            _chat.Notice(who + " is handling " + said + ".");
            Log.Line("bridge: " + what + " -> " + who);
        }

        /// <summary>
        /// The checks the other plugins own: insurance and registration out of StopThePed's records,
        /// and whether somebody has been drinking or using. Answered as text, from their data rather
        /// than from anything invented here.
        /// </summary>
        private void BridgeCheck(string what)
        {
            var bridge = Plugin.Bridge;
            if (bridge == null) { _chat.Error("No framework bridge is running."); return; }

            bridge.Ensure();

            if (what == "breath" || what == "drugs")
            {
                var ped = Subject(6f);
                if (ped == null) { _chat.Error("Nobody close enough to test."); return; }

                var answer = what == "breath" ? bridge.Alcohol(ped) : bridge.Drugs(ped);
                if (answer == null) { _chat.Error("StopThePed is not running - that check is its own."); return; }

                _chat.Notice((what == "breath" ? "Breath check: " : "Drug check: ") + answer + ".");
                Log.Line("bridge: " + what + " -> " + answer);
                return;
            }

            var vehicle = _api.PulloverVehicle();
            if (vehicle == null) vehicle = _api.NearestVehicle(12f);
            if (vehicle == null) { _chat.Error("No vehicle close enough - pull one over, or stand beside one."); return; }

            var status = bridge.VehicleStatus(vehicle, what == "insurance");
            if (status == null) { _chat.Error("StopThePed is not running - that check is its own."); return; }

            string plate = null;
            try { plate = vehicle.LicensePlate; } catch { }

            _chat.Notice((what == "insurance" ? "Insurance" : "Registration") + " on " +
                         (string.IsNullOrEmpty(plate) ? "that vehicle" : plate) + ": " + status + ".");
            Log.Line("bridge: " + what + " -> " + status);
        }

        /// <summary>Which other plugins are here, and what each one adds.</summary>
        private void ShowBridges()
        {
            var bridge = Plugin.Bridge;
            if (bridge == null) { _chat.Error("No framework bridge is running."); return; }

            bridge.Ensure();

            _chat.Notice("Other plugins this can drive, and what each one adds:");
            _chat.Notice("  StopThePed          " + (bridge.HasStopThePed
                ? "yes - PIT, coroner, animal control, radio checks, insurance and registration"
                : "no"));
            _chat.Notice("  Ultimate Backup     " + (bridge.HasUltimateBackup
                ? "yes - K9, spike strips, road blocks, panic, units by type"
                : "no"));
            _chat.Notice("  Policing Redefined  " + (bridge.HasPolicingRedefined
                ? "yes - units by type, air support, felony stops, group backup"
                : "no"));
            _chat.Notice("  A missing one costs only its own commands. /k9 /spikes /pit /insurance /breath ...");

            Log.Line("bridges: " + bridge.Describe());
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
            _chat.Notice("  Civilian: /jobs [filter]  /job <name>  -  DriverJobs V's work, what it pays, where it starts");
            _chat.Notice("  Quick:    /yes accept  /no decline  (or just type 'accept')   /talk  whoever is nearest");
            _chat.Notice("  Stops:    /stop  /endstop  /tow   (or pull over with LSPDFR and it is picked up)");
            _chat.Notice("            /id  /frisk  /cuff  /detain  /release  /record  /owner  /transport");
            _chat.Notice("            (all of those act on the driver you stopped)");
            _chat.Notice("  Scene:    /backup [swat|air|state|ems|fire|transport|code2]  /ems  /fire  /zone");
            _chat.Notice("  Car:      /lock  /unlock  /engine [off]  /trunk  /hood  /doors  /repair  /veh <model>");
            _chat.Notice("  Records:  /mdt  /person [name]  /plate [plate]  /warrant [name]  /bolo  /arrest  /cite <name> <offence>");
            _chat.Notice("  Pursuit:  /pursuit  /calledin  /endpursuit  /panic  /911 <details>");
            _chat.Notice("  Box:      /pos <corner>  /margin <px>  /ui  /font  /fontsize  /lines  /key  /clear");
            _chat.Notice("  Typing:   /typing  whether the other plugins can see what you type, and what it found");
            _chat.Notice("  Install:  /plugins  what LSPDFR actually loaded, and what it did not");
            _chat.Notice("  Open the box with " + Plugin.OpenKeyDescription + ", or / to start a command.");
            _chat.Notice("  Chatter:  /chatter quiet|brief|full  - how much of dispatch's traffic you see.");
            _chat.Notice("  Frameworks: /bridges shows what the other plugins can do, and which are running");
            _chat.Notice("            /k9  /spikes  /roadblock  /pit  /felony  /coroner  /animal  /dismiss");
            _chat.Notice("            /insurance  /reg  /breath  /drugs  /platecheck  /pedcheck");
        }
    }
}
