using System;
using System.Collections.Generic;
using Rage;
using Rage.Native;
using TextDispatch.Chat;
using TextDispatch.Dialogue;
using TextDispatch.Lspdfr;
using TextDispatch.Records;

namespace TextDispatch.Commands
{
    /// <summary>
    /// StopThePed's work, done by TextDispatch itself: a pat-down that says what it found, insurance,
    /// registration and drug checks, escorting and seating somebody, sending them off, and the coroner and
    /// animal control. When StopThePed is installed its own data still answers the checks it owns, so the
    /// two never disagree; without it, these answer from TextDispatch's records.
    /// </summary>
    internal sealed partial class CommandRouter
    {
        // ------------------------------------------------------------------ pat-down

        private static readonly string[] Pockets =
        {
            "a wallet", "a phone", "house keys", "car keys", "a lighter", "loose change", "a pack of gum", "earbuds",
            "a receipt", "a bus pass", "a vape pen", "a pack of cigarettes", "a folded letter", "sunglasses"
        };

        private static readonly string[] PocketContraband =
        {
            "a small bag of white powder", "a folding knife with a four-inch blade", "a bag of pills with no label",
            "a baggie of cannabis", "a set of brass knuckles", "three credit cards in other people's names",
            "a glass pipe with residue", "a loaded revolver in the waistband"
        };

        /// <summary>
        /// /frisk: pat them down and say what is in their pockets. What they carry is decided once, from who
        /// they are: LSPDFR's own contraband flag first, then their record - and what is illegal goes into
        /// evidence with the charge it is.
        /// </summary>
        private void PatDown()
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough to pat down."); return; }

            var handle = unchecked((int)ped.Handle.Value);
            var person = PersonInFront();
            var rng = new Random(unchecked(handle * 2713 + 5));

            bool lspdfr = false;
            try { lspdfr = _api.PedCarryingContraband(ped); } catch { }
            var chance = 8 + (person.Priors.Count > 0 ? 15 : 0) + (person.Wanted ? 20 : 0);
            var illegal = lspdfr || rng.Next(100) < chance;

            var found = new List<string>();
            var count = 2 + rng.Next(3);
            while (found.Count < count) { var item = Pockets[rng.Next(Pockets.Length)]; if (!found.Contains(item)) found.Add(item); }
            string contraband = illegal ? PocketContraband[rng.Next(PocketContraband.Length)] : null;

            // Seen as well as read: they put their hands up, the officer goes over them.
            string ignored;
            PedActions.Perform(_api, ped, ComplyAction.HandsUp, out ignored);
            PedActions.OfficerAction(ped, "CODE_HUMAN_POLICE_INVESTIGATE", 4000);
            _chat.Me("You", "have them turn around, hands on the head, and pat them down - waistband, pockets, ankles.");
            _chat.Do("In their pockets: " + string.Join(", ", found.ToArray()) + (contraband != null ? " - and " + contraband + "." : ". Nothing illegal."));
            _records.Act(person, _dispatch.ReportCallName, "patted them down and found " + string.Join(", ", found.ToArray()) + (contraband != null ? " and " + contraband : ""));

            if (contraband != null)
            {
                _records.AddEvidence(person, contraband + ", found on their person in a pat-down", _dispatch.ReportCallName, false);
                var charge = PenalCode.Find(contraband.Contains("knife") ? "concealed knife" : contraband.Contains("revolver") ? "unlicensed firearm"
                                           : contraband.Contains("credit") ? "fraud" : contraband.Contains("brass") ? "prohibited weapon"
                                           : contraband.Contains("pipe") ? "paraphernalia" : contraband.Contains("cannabis") ? "possession of cannabis" : "drug possession");
                _chat.Notice("  Logged into evidence." + (charge != null ? " That is " + charge.Line() + " - '/charge " + charge.Code + "', then '/arrest'." : ""));
            }
        }

        // ------------------------------------------------------------------ the checks

        /// <summary>Insurance, registration, breath or drugs, answered here when StopThePed is not installed.</summary>
        private bool NativeCheck(string what)
        {
            if (what == "breath") { BreathTest(); return true; }
            if (what == "drugs") { DrugTest(); return true; }

            var vehicle = _api.PulloverVehicle() ?? _api.NearestVehicle(12f);
            if (vehicle == null) { _chat.Error("No vehicle close enough - pull one over, or stand beside one."); return true; }
            string plate = null, model = null;
            try { plate = vehicle.LicensePlate.Trim(); model = vehicle.Model.Name; } catch { }
            var record = _records.EnsureVehicle(plate, model);
            if (record == null) { _chat.Error("That vehicle has no plate to check."); return true; }

            if (what == "insurance")
                _chat.Write(ChatChannel.Dispatch, "[DISPATCH]", "Insurance on " + record.Plate + ": " + (record.Insured ? "valid, on file with the insurer" : "NONE ON FILE - it is uninsured") + ".");
            else
                _chat.Write(ChatChannel.Dispatch, "[DISPATCH]", "Registration on " + record.Plate + ": " + Registration(record) + ", registered to " + record.OwnerName + ".");
            _records.Act(_records.LastSubject, _dispatch.ReportCallName, "ran the " + what + " on " + record.Plate);
            return true;
        }

        /// <summary>The registration's status, decided from the plate: valid most of the time.</summary>
        private static string Registration(VehicleRecord record)
        {
            if (record.ReportedStolen) return "VALID, but the vehicle is REPORTED STOLEN";
            var roll = new Random(unchecked(RecordsLedger.PlateKey(record.Plate).GetHashCode() * 19 + 1)).Next(100);
            return roll < 85 ? "valid" : roll < 96 ? "EXPIRED" : "NOT REGISTERED";
        }

        /// <summary>/drugs without StopThePed: a saliva swab, decided once per person, kept in evidence.</summary>
        private void DrugTest()
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough to test."); return; }
            var handle = unchecked((int)ped.Handle.Value);
            var person = PersonInFront();

            var rng = new Random(unchecked(handle * 389 + 23));
            var chance = 7 + (person.Priors.Exists(p => p.IndexOf("drug", StringComparison.OrdinalIgnoreCase) >= 0) ? 30 : 0);
            try { if (_api.PedCarryingContraband(ped)) chance += 25; } catch { }
            var positive = rng.Next(100) < chance;
            var drug = new[] { "cannabis", "cocaine", "methamphetamine", "opiates", "benzodiazepines" }[rng.Next(5)];

            _chat.Me("You", "swab the inside of their cheek and wait for the strip to develop.");
            _chat.Do("The test strip reads " + (positive ? "POSITIVE for " + drug : "negative") + ".");
            _records.AddEvidence(person, "roadside drug test: " + (positive ? "positive for " + drug : "negative"), _dispatch.ReportCallName, false);
            _records.Act(person, _dispatch.ReportCallName, "administered a roadside drug test, which was " + (positive ? "positive for " + drug : "negative"));
            if (positive) _chat.Notice("  Driving on it is a DUI (PC 7-01): '/charge dui', '/arrest'.");
        }

        /// <summary>The radio checks without StopThePed: the same records lookups, asked of dispatch.</summary>
        private bool NativeRadioCheck(string what)
        {
            if (what == "platecheck")
            {
                var vehicle = _api.PulloverVehicle() ?? _api.NearestOtherVehicle(15f);
                string plate = null;
                try { if (vehicle != null) plate = vehicle.LicensePlate.Trim(); } catch { }
                if (plate == null) { _chat.Error("No plate in front of you to run."); return true; }
                _chat.Radio("You: " + _dispatch.Unit + ", run a plate for me - " + plate + ".");
                _dispatch.Say("run " + plate);
                return true;
            }
            var person = Subject(8f) != null ? PersonInFront() : _records.LastSubject;
            if (person == null) { _chat.Error("Nobody to run."); return true; }
            _chat.Radio("You: " + _dispatch.Unit + ", run a person for me - " + person.Name + ".");
            _dispatch.Say("person " + person.Name);
            return true;
        }

        // ------------------------------------------------------------------ moving people

        /// <summary>/follow, /escort: they walk with you - behind you, or at your side holding their arm.</summary>
        private void Follow(bool escort)
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough."); return; }
            var player = Game.LocalPlayer.Character;
            try
            {
                ped.Tasks.Clear();
                if (escort) NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(ped, player, 0.6f, 0.3f, 0f, 1.2f, -1, 0.5f, true);
                else NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(ped, player, 0f, -1.4f, 0f, 1.4f, -1, 1.0f, true);
                ped.BlockPermanentEvents = true;
            }
            catch (Exception ex) { Log.Error("follow", ex); _chat.Error("They would not move - the log has why."); return; }
            _chat.Me("You", escort ? "take them by the arm and walk them along." : "tell them to walk with you.");
            _records.Act(PersonInFront(), _dispatch.ReportCallName, escort ? "escorted them" : "had them follow");
        }

        private Ped _held;

        /// <summary>/grab: hold them by the arm - attached at your side, they go where you go. /letgo puts them down.</summary>
        private void Grab()
        {
            if (_held != null && _held.Exists()) { LetGo(); return; }
            var ped = Subject(3f);
            if (ped == null) { _chat.Error("Nobody close enough to take hold of - get within arm's reach."); return; }
            string detail;
            if (!PedActions.Grab(ped, out detail)) { _chat.Error("Can't take hold of them - " + detail + "."); return; }
            _held = ped;
            _chat.Me("You", "take a firm hold of their arm.");
            _chat.Notice("  Walk and they come with you. '/letgo' (or /grab again) lets go; '/seat' puts them in the car.");
            _records.Act(PersonInFront(), _dispatch.ReportCallName, "took hold of them and escorted them");
        }

        private void LetGo()
        {
            if (_held == null || !_held.Exists()) { _chat.Notice("You are not holding anybody."); _held = null; return; }
            string detail;
            PedActions.LetGo(_held, out detail);
            _chat.Me("You", "let go of their arm.");
            _held = null;
        }

        /// <summary>/stay: they stop where they are and wait.</summary>
        private void Stay()
        {
            var ped = Subject(8f);
            if (ped == null) { _chat.Error("Nobody close enough."); return; }
            try { ped.Tasks.Clear(); NativeFunction.Natives.TASK_STAND_STILL(ped, -1); }
            catch (Exception ex) { Log.Error("stay", ex); }
            _chat.Me("You", "tell them to stay right there.");
        }

        /// <summary>/seat: put them in the back of your car (or the nearest), a free rear seat.</summary>
        private void Seat()
        {
            // Somebody held is let go first, then put in.
            if (_held != null && _held.Exists()) { string let; PedActions.LetGo(_held, out let); }
            var ped = _held != null && _held.Exists() ? _held : Subject(8f);
            _held = null;
            if (ped == null) { _chat.Error("Nobody close enough."); return; }
            var player = Game.LocalPlayer.Character;
            Vehicle car = null;
            try { car = player.LastVehicle; } catch { }
            if (car == null || !car.Exists() || car.Position.DistanceTo(player.Position) > 20f) car = _api.NearestOtherVehicle(15f);
            if (car == null || !car.Exists()) { _chat.Error("No car close enough to put them in."); return; }

            var seat = -2;
            foreach (var s in new[] { 1, 2, 0 })
                try { if (car.IsSeatFree(s)) { seat = s; break; } } catch { }
            if (seat == -2) { _chat.Error("No free seat in that car."); return; }

            try
            {
                ped.Tasks.Clear();
                NativeFunction.Natives.TASK_ENTER_VEHICLE(ped, car, 20000, seat, 1.0f, 1, 0);
                ped.BlockPermanentEvents = true;
            }
            catch (Exception ex) { Log.Error("seat", ex); _chat.Error("They could not be seated - the log has why."); return; }
            _chat.Me("You", "open the rear door, guard their head, and sit them in the back.");
            _records.Act(PersonInFront(), _dispatch.ReportCallName, "placed them in the rear of the vehicle");
        }

        /// <summary>/unseat: take them out of the car again.</summary>
        private void Unseat()
        {
            var player = Game.LocalPlayer.Character;
            Ped ped = null;
            try
            {
                foreach (var entity in World.GetEntities(player.Position, 8f, GetEntitiesFlags.ConsiderHumanPeds | GetEntitiesFlags.ExcludePlayerPed))
                {
                    var p = entity as Ped;
                    if (p != null && p.Exists() && p.IsInAnyVehicle(false) && !_api.PedIsCop(p)) { ped = p; break; }
                }
            }
            catch { }
            if (ped == null) { _chat.Error("Nobody in a car beside you."); return; }
            try { NativeFunction.Natives.TASK_LEAVE_VEHICLE(ped, ped.CurrentVehicle, 0); }
            catch (Exception ex) { Log.Error("unseat", ex); }
            _chat.Me("You", "open the door and help them out.");
        }

        /// <summary>
        /// /taxi: send them on their way - the stop is over, a cab takes them. Released from LSPDFR if they
        /// were held, and they walk off. For somebody you are done with but should not leave at the roadside.
        /// </summary>
        private void Taxi()
        {
            var ped = Subject(8f);
            if (ped == null) { _chat.Error("Nobody close enough."); return; }
            if (_api.PedArrested(ped)) { _chat.Error("They are in custody - '/release' first, or '/transport'."); return; }
            try
            {
                _api.ReleasePed(ped);
                ped.Tasks.Clear();
                ped.BlockPermanentEvents = false;
                NativeFunction.Natives.TASK_WANDER_STANDARD(ped, 10f, 10);
                ped.Dismiss();
            }
            catch (Exception ex) { Log.Error("taxi", ex); }
            _chat.Me("You", "call them a cab and tell them they're free to go.");
            _chat.Do("A taxi pulls up a minute later and takes them away.");
            _records.Act(PersonInFront(), _dispatch.ReportCallName, "released them and arranged a taxi");
        }

        // ------------------------------------------------------------------ services

        /// <summary>The coroner or animal control without StopThePed: a van arrives and takes what is there.</summary>
        private bool NativeService(string what)
        {
            var player = Game.LocalPlayer.Character;
            var targets = new List<Ped>();
            try
            {
                foreach (var entity in World.GetEntities(player.Position, 40f, GetEntitiesFlags.ConsiderAllPeds | GetEntitiesFlags.ExcludePlayerPed))
                {
                    var ped = entity as Ped;
                    if (ped == null || !ped.Exists()) continue;
                    if (what == "coroner" ? ped.IsDead && ped.IsHuman : !ped.IsHuman) targets.Add(ped);
                }
            }
            catch (Exception ex) { Log.Error(what, ex); }

            var service = what == "coroner" ? "the coroner" : "animal control";
            if (targets.Count == 0) { _chat.Error(what == "coroner" ? "No bodies within 40 m." : "No animals within 40 m."); return true; }

            _chat.Radio("You: " + _dispatch.Unit + ", I need " + service + " at my location.");
            _chat.Write(ChatChannel.Dispatch, "[DISPATCH]", "Copy, " + service + " is en route to you, about a minute out.");
            _records.IncidentNote(service + " requested");
            var count = targets.Count;
            GameFiber.StartNew(() =>
            {
                GameFiber.Sleep(45000);
                foreach (var ped in targets)
                    try { if (ped.Exists()) ped.Delete(); } catch { }
                _chat.Write(ChatChannel.Dispatch, "[DISPATCH]", (what == "coroner" ? "The coroner has removed " + count + " bod" + (count == 1 ? "y" : "ies")
                                                                                    : "Animal control has taken " + count + " animal" + (count == 1 ? "" : "s")) + " from your scene.");
            }, "TextDispatch " + what);
            return true;
        }
    }
}
