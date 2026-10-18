using System;
using System.Collections.Generic;
using Rage;
using Rage.Native;
using TextDispatch.Chat;
using TextDispatch.Dialogue;
using TextDispatch.Records;

namespace TextDispatch.Commands
{
    /// <summary>
    /// The EMS job and the fire job, each behind one command. /ems is the paramedic's work on a patient -
    /// vitals, treatment, the hospital, the report; /fire is the crew's on a fire - sizing it up, putting it
    /// out, searching, the report. Calling one of them as backup is /ems call and /fire call, or /ambulance
    /// and /firedept as before.
    /// </summary>
    internal sealed partial class CommandRouter
    {
        // ------------------------------------------------------------------ EMS

        private static readonly KeyValuePair<string, Vector3>[] Hospitals =
        {
            new KeyValuePair<string, Vector3>("Pillbox Hill Medical Center", new Vector3(298f, -584f, 43f)),
            new KeyValuePair<string, Vector3>("Mount Zonah Medical Center", new Vector3(-449f, -340f, 34f)),
            new KeyValuePair<string, Vector3>("Central Los Santos Medical Center", new Vector3(340f, -1396f, 32f)),
            new KeyValuePair<string, Vector3>("St Fiacre Hospital", new Vector3(1152f, -1528f, 34f)),
            new KeyValuePair<string, Vector3>("Sandy Shores Medical Center", new Vector3(1839f, 3672f, 34f)),
            new KeyValuePair<string, Vector3>("Paleto Bay Care Center", new Vector3(-247f, 6331f, 32f)),
        };

        /// <summary>/ems [vitals|treat|hospital|pcr|call]: the EMS job.</summary>
        private void EmsJob(string argument)
        {
            var text = (argument ?? "").Trim();
            var split = text.IndexOf(' ');
            var verb = (split < 0 ? text : text.Substring(0, split)).ToLowerInvariant();
            var rest = split < 0 ? "" : text.Substring(split + 1).Trim();

            switch (verb)
            {
                case "vitals": case "assess": Vitals(); return;
                case "treat": case "aid": Treat(rest); return;
                case "pcr": case "report": PatientReport(); return;
                case "hospital": case "transport": Hospital(); return;
                case "call": case "backup": case "ambulance": Backup("ems"); return;
                case "":
                    _chat.Notice("EMS - the paramedic's job, on the patient in front of you:");
                    _chat.Notice("  /ems vitals          pulse, BP, breathing, SpO2, GCS, pupils, skin");
                    _chat.Notice("  /ems treat <what>    bleeding, tourniquet, oxygen, splint, cpr, aed, narcan, collar, recovery, burns");
                    _chat.Notice("  /ems hospital        the nearest hospital, how far, and a route on the map");
                    _chat.Notice("  /ems pcr             a patient care report from what you did, into the box to finish");
                    _chat.Notice("  /ems call            send an ambulance as backup (or /ambulance)");
                    var agency = SafeAgency();
                    if (agency != null && agency.StartsWith("lsfd", StringComparison.OrdinalIgnoreCase))
                        _chat.Notice("  On duty as " + agency + ". Medical callouts come to you; '/calls medical' lists them.");
                    return;
            }
            _chat.Error("'/ems " + verb + "' is not part of the EMS job. /ems lists it.");
        }

        private string SafeAgency()
        {
            try { return _api.CurrentAgency(); }
            catch { return null; }
        }

        private static Blip _routeBlip;

        /// <summary>The nearest hospital: its name, how far, and a GPS route - for taking the patient in.</summary>
        private void Hospital()
        {
            Vector3 at;
            try { at = Game.LocalPlayer.Character.Position; }
            catch { return; }

            var best = Hospitals[0];
            foreach (var hospital in Hospitals)
                if (at.DistanceTo(hospital.Value) < at.DistanceTo(best.Value)) best = hospital;
            var metres = at.DistanceTo(best.Value);

            try
            {
                if (_routeBlip != null && _routeBlip.Exists()) _routeBlip.Delete();
                _routeBlip = new Blip(best.Value) { Color = System.Drawing.Color.Red, Name = best.Key };
                _routeBlip.IsRouteEnabled = true;
            }
            catch (Exception ex) { Log.Error("hospital blip", ex); }

            _chat.Write(ChatChannel.Dispatch, "[EMS]", "Nearest is " + best.Key + ", " + (metres >= 1000 ? (metres / 1000f).ToString("0.0") + " km" : metres.ToString("0") + " m") +
                        ". Route is on your map - let them know you're inbound.");
            var person = _records.LastSubject;
            if (person != null) _records.Act(person, _dispatch.ReportCallName, "transported them to " + best.Key);
        }

        // ------------------------------------------------------------------ fire

        /// <summary>/fire [sizeup|attack|search|overhaul|report|call]: the fire job.</summary>
        private void FireJob(string argument)
        {
            var text = (argument ?? "").Trim();
            var split = text.IndexOf(' ');
            var verb = (split < 0 ? text : text.Substring(0, split)).ToLowerInvariant();

            switch (verb)
            {
                case "sizeup": case "size": case "size-up": case "assess": FireSizeUp(); return;
                case "attack": case "extinguish": case "out": case "hose": FireAttack(); return;
                case "search": case "primary": FireSearch(); return;
                case "overhaul": case "checks": FireOverhaul(); return;
                case "report": FireReport(); return;
                case "call": case "backup": case "truck": Backup("fire"); return;
                case "":
                    _chat.Notice("Fire - the crew's job, on the fire in front of you:");
                    _chat.Notice("  /fire sizeup         how many fires, how big, what is burning, and anybody in danger");
                    _chat.Notice("  /fire attack         put it out - a line on the fire within 25 m");
                    _chat.Notice("  /fire search         a primary search for anybody inside or nearby");
                    _chat.Notice("  /fire overhaul       check for hot spots once it is out");
                    _chat.Notice("  /fire report         a fire incident report from what you did, into the box to finish");
                    _chat.Notice("  /fire call           send the fire department as backup (or /firedept)");
                    return;
            }
            _chat.Error("'/fire " + verb + "' is not part of the fire job. /fire lists it.");
        }

        private static int FiresNear(Vector3 at, float radius)
        {
            try { return NativeFunction.Natives.GET_NUMBER_OF_FIRES_IN_RANGE<int>(at.X, at.Y, at.Z, radius); }
            catch { return -1; }
        }

        private int _attacks;

        private void FireSizeUp()
        {
            Vector3 at;
            try { at = Game.LocalPlayer.Character.Position; }
            catch { return; }

            var near = FiresNear(at, 25f);
            var wide = FiresNear(at, 80f);
            if (wide < 0) { _chat.Error("Could not read the fires around you."); return; }

            _chat.Me("You", "stop at the edge of the scene and take it in - the smoke, the flames, the people.");
            if (wide == 0) { _chat.Do("No fire. Whatever was burning is out, or it is somewhere else."); return; }

            var cars = 0;
            var people = 0;
            try
            {
                foreach (var entity in World.GetEntities(at, 30f, GetEntitiesFlags.ConsiderAllVehicles))
                {
                    var car = entity as Vehicle;
                    if (car != null && car.Exists() && car.IsOnFire) cars++;
                }
                foreach (var entity in World.GetEntities(at, 30f, GetEntitiesFlags.ConsiderHumanPeds | GetEntitiesFlags.ExcludePlayerPed))
                {
                    var ped = entity as Ped;
                    if (ped != null && ped.Exists() && ped.IsAlive && !_api.PedIsCop(ped)) people++;
                }
            }
            catch { }

            var size = wide >= 12 ? "a large, spreading fire" : wide >= 5 ? "a working fire" : "a small fire";
            _chat.Do(size + ": " + wide + " seats of fire within 80 m, " + near + " within reach" +
                     (cars > 0 ? ", " + cars + " vehicle(s) burning" : "") +
                     (people > 0 ? ", " + people + " people close by" : ", nobody in sight") + ".");
            _records.IncidentNote("size-up: " + size + ", " + wide + " fires");
            _records.Act(null, _dispatch.ReportCallName, "sized up the scene - " + size + ", " + wide + " seats of fire" + (cars > 0 ? ", " + cars + " vehicle(s) involved" : ""));
            if (wide >= 12) _chat.Notice("  That needs more than one crew - '/fire call' for another truck.");
        }

        private void FireAttack()
        {
            Vector3 at;
            try { at = Game.LocalPlayer.Character.Position; }
            catch { return; }

            var near = FiresNear(at, 25f);
            if (near <= 0) { _chat.Error(near == 0 ? "No fire within 25 m - get closer, or it is already out." : "Could not read the fires around you."); return; }

            _chat.Me("You", "pull a line off the truck, open the nozzle and hit the base of the fire.");
            try
            {
                NativeFunction.Natives.STOP_FIRE_IN_RANGE(at.X, at.Y, at.Z, 25f);
                foreach (var entity in World.GetEntities(at, 25f, GetEntitiesFlags.ConsiderAllVehicles))
                {
                    var car = entity as Vehicle;
                    if (car != null && car.Exists() && car.IsOnFire) NativeFunction.Natives.STOP_ENTITY_FIRE(car);
                }
            }
            catch (Exception ex) { Log.Error("fire attack", ex); }

            _attacks++;
            var left = FiresNear(at, 80f);
            _chat.Do("The flames die back under the water" + (left > 0 ? " - " + left + " still burning further off." : " - knocked down."));
            _records.Act(null, _dispatch.ReportCallName, "put a line on the fire and knocked down " + near + " seat(s) of fire");
            _records.IncidentNote("knock-down");
            if (left <= 0) _chat.Notice("  Knocked down. '/fire overhaul' for hot spots, '/fire search' for anybody inside, then '/fire report'.");
        }

        private void FireSearch()
        {
            Vector3 at;
            try { at = Game.LocalPlayer.Character.Position; }
            catch { return; }

            _chat.Me("You", "go in low with a partner, right-hand search, calling out as you go.");
            var found = new List<string>();
            try
            {
                foreach (var entity in World.GetEntities(at, 20f, GetEntitiesFlags.ConsiderHumanPeds | GetEntitiesFlags.ExcludePlayerPed))
                {
                    var ped = entity as Ped;
                    if (ped == null || !ped.Exists() || _api.PedIsCop(ped)) continue;
                    var handle = unchecked((int)ped.Handle.Value);
                    found.Add(Identities.NameFor(handle) + (ped.IsDead ? " (unresponsive)" : ped.Health < ped.MaxHealth * 0.7f ? " (hurt)" : ""));
                }
            }
            catch { }

            _chat.Do(found.Count == 0 ? "Primary search negative - nobody inside or nearby." : "Found " + found.Count + ": " + string.Join(", ", found.ToArray()) + ".");
            _records.Act(null, _dispatch.ReportCallName, found.Count == 0 ? "carried out a primary search - negative" : "carried out a primary search and found " + string.Join(", ", found.ToArray()));
            if (found.Count > 0) _chat.Notice("  '/ems vitals' on anybody hurt, '/ems call' for an ambulance.");
        }

        private void FireOverhaul()
        {
            Vector3 at;
            try { at = Game.LocalPlayer.Character.Position; }
            catch { return; }

            var left = FiresNear(at, 40f);
            _chat.Me("You", "pull the debris apart and sweep it with the thermal camera.");
            if (left > 0)
            {
                _chat.Do("A hot spot flares - " + left + " still burning. '/fire attack' again.");
                return;
            }
            _chat.Do("No hot spots. The scene is cold.");
            _records.Act(null, _dispatch.ReportCallName, "completed overhaul - no hot spots, the scene is cold");
            _records.IncidentNote("overhaul complete");
        }

        private void FireReport()
        {
            var text = new System.Text.StringBuilder("Fire incident report. ");
            if (_dispatch.ReportCallName != null) text.Append("I responded to ").Append(_dispatch.ReportCallName).Append(". ");
            var any = false;
            foreach (var action in _records.Actions)
            {
                if (action.PersonId != null || DateTime.Now.Ticks - action.Ticks > TimeSpan.FromHours(1).Ticks) continue;
                var t = action.Text ?? "";
                if (!t.StartsWith("sized up") && !t.StartsWith("put a line") && !t.StartsWith("carried out a primary") && !t.StartsWith("completed overhaul")) continue;
                text.Append(new DateTime(action.Ticks).ToString("HH:mm")).Append(" - I ").Append(t).Append(". ");
                any = true;
            }
            if (!any) { _chat.Error("Nothing done on a fire to report yet - '/fire sizeup' and '/fire attack'."); return; }
            text.Append("The scene was left safe.");

            _chat.IsOpen = true;
            _chat.Input = "/report general: " + text;
            _chat.Scroll = 0;
            _chat.Notice("Fire incident report drafted in the box - add the cause if you know it, then Enter files it.");
        }
    }
}
