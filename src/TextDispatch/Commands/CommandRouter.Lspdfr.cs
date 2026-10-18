using System;
using System.Collections.Generic;
using Rage;
using TextDispatch.Chat;
using TextDispatch.Dialogue;
using TextDispatch.Lspdfr;
using TextDispatch.Records;

namespace TextDispatch.Commands
{
    /// <summary>
    /// The LSPDFR half of the text set: sobriety tests, booking at the station, the dispatch screen and the
    /// unit board, collision reports, the checks run together, and the passengers.
    /// </summary>
    internal sealed partial class CommandRouter
    {
        // ------------------------------------------------------------------ sobriety

        /// <summary>
        /// /fst [hgn|walk|stand]: the standardised field sobriety tests on whoever is in front of you - all
        /// three, or the one named - with the clues written out the way they go in a DUI report.
        /// </summary>
        private void FieldSobriety(string argument)
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough to test."); return; }
            var handle = unchecked((int)ped.Handle.Value);
            var person = PersonInFront();

            var which = (argument ?? "").Trim().ToLowerInvariant();
            var tests = which.StartsWith("h") || which.StartsWith("eye") ? new[] { "hgn" }
                      : which.StartsWith("w") ? new[] { "walk" }
                      : which.StartsWith("s") || which.StartsWith("o") || which.StartsWith("leg") ? new[] { "stand" }
                      : new[] { "hgn", "walk", "stand" };

            int total = 0, possible = 0;
            foreach (var test in tests)
            {
                int clues, of;
                var result = Sobriety.Test(handle, test, out clues, out of);
                total += clues;
                possible += of;
                _chat.Me("You", test == "hgn" ? "hold a pen up and have them follow it with their eyes."
                              : test == "walk" ? "have them walk nine steps heel to toe along a line, turn, and walk back."
                              : "have them stand on one leg and count out loud.");
                _chat.Do(result);
                _records.Act(person, _dispatch.ReportCallName, "administered a field sobriety test - " + result.TrimEnd('.'));
            }

            if (tests.Length == 3)
            {
                var impaired = total >= 7;
                _chat.Notice("  " + total + " of " + possible + " clues across the three tests - " +
                             (impaired ? "consistent with impairment. '/pbt' for a breath reading, then '/charge dui' and '/arrest'."
                                       : total >= 3 ? "borderline. '/pbt' settles it." : "no sign of impairment."));
            }
        }

        /// <summary>/pbt: a preliminary breath test on whoever is in front of you. The reading goes into evidence.</summary>
        private void BreathTest()
        {
            var ped = Subject(6f);
            if (ped == null) { _chat.Error("Nobody close enough to test."); return; }
            var handle = unchecked((int)ped.Handle.Value);
            var person = PersonInFront();

            var reading = Sobriety.Breath(handle);
            _chat.Me("You", "hold out the breath tester and have them blow until it beeps.");
            _chat.Do("The screen reads " + reading.ToString("0.00") + "." + (reading >= Sobriety.Limit ? " Over the legal limit of 0.08." : ""));
            _records.AddEvidence(person, "preliminary breath test: " + reading.ToString("0.00") + " BAC", _dispatch.ReportCallName, false);
            _records.Act(person, _dispatch.ReportCallName, "administered a preliminary breath test, which read " + reading.ToString("0.00"));
            if (reading >= Sobriety.Limit)
                _chat.Notice("  Over the limit: '/charge dui' (PC 7-01) and '/arrest'. The reading is in evidence.");
        }

        // ------------------------------------------------------------------ booking

        /// <summary>
        /// /book [name]: process an arrest at the station - booking number, charges, prints, photographs,
        /// property, a cell. Needs a station; LSPDFR's own drop-off can still take them after.
        /// </summary>
        private void Book(string argument)
        {
            var station = DispatchService.StationHere();
            if (station == null)
            {
                _chat.Error("Booking is done at a station - Mission Row, Vespucci, Davis, La Mesa, Vinewood, Rockford Hills, Del Perro, Sandy Shores, Paleto Bay or Bolingbroke.");
                var waiting = _records.UnbookedArrests();
                if (waiting.Count > 0) _chat.Notice("  Waiting to be booked: " + string.Join(", ", waiting.ConvertAll(f => f.Name).ToArray()) + ".");
                return;
            }

            var name = (argument ?? "").Trim();
            var queue = new List<FollowUp>();
            if (name.Length > 0)
            {
                var person = _records.FindPerson(name);
                var arrest = person == null ? null : _records.Unbooked(person);
                if (arrest == null) { _chat.Error(person == null ? "No record for '" + name + "'." : person.Name + " has no arrest waiting to be booked."); return; }
                queue.Add(arrest);
            }
            else queue.AddRange(_records.UnbookedArrests());

            if (queue.Count == 0) { _chat.Notice("Nobody to book. '/arrest' puts somebody in custody first."); return; }

            foreach (var arrest in queue)
            {
                var person = _records.FindPerson(arrest.Name);
                if (person == null) continue;
                var booking = _records.Book(person, arrest, station, arrest.Call);

                _chat.Me("You", "walk " + person.Name + " in through the sally port to the booking desk.");
                _chat.Write(ChatChannel.Dispatch, "[BOOKING " + booking.Number + "]", person.Name + ", " + person.Age + " - " + station + " - " + booking.When);
                _chat.Notice("  Charges: " + (arrest.Charges != null && arrest.Charges.Count > 0 ? PenalCode.Describe(arrest.Charges) : arrest.Offence));
                _chat.Notice("  Prints taken and submitted. Photographs: front and profile.");
                _chat.Notice("  Property bagged: " + string.Join(", ", booking.Property.ToArray()) + ".");
                _chat.Notice("  Placed in " + booking.Cell + " to await arraignment." +
                             (arrest.ReportFiled ? "" : " The DA still needs your report - /report."));
                Log.Line("booking: " + booking.Number + " " + person.Name + " at " + station);
            }
        }

        // ------------------------------------------------------------------ the dispatch screen

        /// <summary>/cad [number]: the call in progress and the recent ones, or one incident's log.</summary>
        private void Cad(string argument)
        {
            var number = (argument ?? "").Trim();
            if (number.Length > 0)
            {
                var incident = _records.FindIncident(number);
                if (incident == null) { _chat.Error("No incident " + number + ". /cad lists the recent ones."); return; }
                _chat.Notice("Incident " + incident.Number + "  " + incident.Priority + "  " + incident.Call + (incident.Zone != null ? "  (" + incident.Zone + ")" : ""));
                foreach (var line in incident.Log) _chat.Notice("  " + line);
                if (incident.Closed == null) _chat.Notice("  OPEN");
                foreach (var report in _records.Reports)
                    if (report.Incident == incident.Number) _chat.Notice("  report #" + report.Number + " - " + (report.Subject ?? "general"));
                return;
            }

            var open = _records.CurrentIncident;
            if (open != null)
            {
                _chat.Notice("ACTIVE  " + open.Number + "  " + open.Priority + "  " + open.Call + "  - opened " + Clock(open.Opened) +
                             (open.OnScene != null ? ", on scene " + Clock(open.OnScene) : ", en route"));
            }
            else _chat.Notice("No active incident." + (_dispatch.NextAutoCallSeconds >= 0 ? " Next call in about " + _dispatch.NextAutoCallSeconds + "s." : ""));

            var shown = 0;
            for (var i = _records.Incidents.Count - 1; i >= 0 && shown < 8; i--)
            {
                var incident = _records.Incidents[i];
                if (incident == open) continue;
                shown++;
                _chat.Notice("  " + incident.Number + "  " + incident.Priority + "  " + Clock(incident.Opened) + "  " + incident.Call +
                             "  - " + (incident.Disposition ?? "open"));
            }
            if (shown == 0 && open == null) _chat.Notice("  Nothing on the screen yet - calls get a number as they come in.");
            else _chat.Notice("  /cad <number> for one incident's log. Reports carry the incident number.");
        }

        private static string Clock(string when)
        {
            DateTime parsed;
            return DateTime.TryParse(when, out parsed) ? parsed.ToString("HH:mm") : when;
        }

        /// <summary>
        /// /board: the units on the street - every police car near you with officers in it, its callsign,
        /// what it is doing and how far off it is. The units sent to you are marked.
        /// </summary>
        private void Board()
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) return;

            var sent = new HashSet<uint>();
            foreach (var unit in _dispatch.Units())
                try { if (unit.Vehicle != null && unit.Vehicle.Exists()) sent.Add(unit.Vehicle.Handle.Value); } catch { }

            var rows = new List<KeyValuePair<float, string>>();
            try
            {
                foreach (var vehicle in World.GetAllVehicles())
                {
                    if (vehicle == null || !vehicle.Exists()) continue;
                    Ped driver = null;
                    try { driver = vehicle.Driver; } catch { }
                    if (driver == null || !driver.Exists() || driver == player || !_api.PedIsCop(driver)) continue;
                    if (player.IsInAnyVehicle(false) && vehicle == player.CurrentVehicle) continue;

                    var metres = vehicle.Position.DistanceTo(player.Position);
                    if (metres > 2500f) continue;

                    var mph = (int)(vehicle.Speed * 2.23694f);
                    bool siren = false;
                    try { siren = vehicle.IsSirenOn; } catch { }
                    var status = siren && mph > 15 ? "code 3, " + mph + " mph"
                               : mph > 3 ? "10-8, patrolling"
                               : "10-6, stationary";
                    var handle = vehicle.Handle.Value;
                    var callsign = (1 + handle % 9) + "-" + Phonetic[(int)(handle / 9 % (uint)Phonetic.Length)] + "-" + (10 + handle % 80);
                    string model = "unit";
                    try { model = vehicle.Model.Name.ToLowerInvariant(); } catch { }
                    string zone = null;
                    try { zone = _api.ZoneAt(vehicle.Position); } catch { }

                    rows.Add(new KeyValuePair<float, string>(metres,
                        callsign.PadRight(11) + status.PadRight(22) + (metres < 1000 ? metres.ToString("0") + " m" : (metres / 1000f).ToString("0.0") + " km") +
                        (zone != null ? ", " + zone : "") + "  (" + model + ")" + (sent.Contains(handle) ? "  <- sent to you" : "")));
                }
            }
            catch (Exception ex) { Log.Error("unit board", ex); }

            var current = _records.CurrentIncident;
            _chat.Notice("Unit board - " + _dispatch.Unit + " (you): " + (string.IsNullOrEmpty(_dispatch.LastStatus) ? "10-8" : _dispatch.LastStatus.ToUpperInvariant()) +
                         (current != null ? ", on " + current.Number + " " + current.Call : ""));
            if (rows.Count == 0) { _chat.Notice("  No other units within 2.5 km."); return; }
            rows.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (var i = 0; i < rows.Count && i < 10; i++) _chat.Notice("  " + rows[i].Value);
            if (rows.Count > 10) _chat.Notice("  ... and " + (rows.Count - 10) + " more further out.");
        }

        private static readonly string[] Phonetic = { "ADAM", "BOY", "CHARLES", "DAVID", "EDWARD", "FRANK", "LINCOLN", "MARY", "OCEAN", "TOM", "UNION", "X-RAY" };

        // ------------------------------------------------------------------ collisions

        /// <summary>
        /// /collision: the damaged cars at a crash, their drivers' statements, and who was at fault - written
        /// into the report box as a collision report for you to finish.
        /// </summary>
        private void Collision()
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) return;

            var cars = new List<Vehicle>();
            try
            {
                foreach (var vehicle in World.GetAllVehicles())
                {
                    if (vehicle == null || !vehicle.Exists() || vehicle.Position.DistanceTo(player.Position) > 40f) continue;
                    if (player.IsInAnyVehicle(false) && vehicle == player.CurrentVehicle) continue;
                    Ped driver = null;
                    try { driver = vehicle.Driver; } catch { }
                    if (driver != null && driver.Exists() && _api.PedIsCop(driver)) continue;
                    if (vehicle.Health < 990 || vehicle.EngineHealth < 990) cars.Add(vehicle);
                }
            }
            catch (Exception ex) { Log.Error("collision", ex); }

            if (cars.Count == 0) { _chat.Error("No damaged vehicles within 40m - stand at the scene of the collision."); return; }
            cars.Sort((a, b) => a.Health.CompareTo(b.Health));
            if (cars.Count > 3) cars.RemoveRange(3, cars.Count - 3);

            var involved = new List<Tuple<Vehicle, PersonRecord, int, string>>();
            foreach (var car in cars)
            {
                string plate = "no plate", model = "vehicle";
                try { plate = car.LicensePlate.Trim(); } catch { }
                try { model = car.Model.Name; } catch { }
                _records.EnsureVehicle(plate, model);

                Ped driver = null;
                try { driver = car.Driver; } catch { }
                if (driver == null || !driver.Exists())
                {
                    // Out of the car and standing by it, as people do after a crash.
                    try
                    {
                        foreach (var ped in World.GetAllPeds())
                            if (ped != null && ped.Exists() && ped != player && !_api.PedIsCop(ped) && ped.Position.DistanceTo(car.Position) < 6f &&
                                ped.LastVehicle == car) { driver = ped; break; }
                    }
                    catch { }
                }

                PersonRecord person = null;
                var handle = 0;
                if (driver != null && driver.Exists())
                {
                    handle = unchecked((int)driver.Handle.Value);
                    person = _records.EnsurePerson(handle, Identities.NameFor(handle));
                    _records.Touch(person);
                    PersonaInfo.Sync(_api, driver, _records, person);
                }
                involved.Add(Tuple.Create(car, person, handle, model + " " + plate));
            }

            // Fault: drink first, then a suspended or no licence, then the car with the worst damage at the
            // front - the one that hit rather than was hit is the usual answer.
            var fault = involved[0];
            var why = "the damage pattern";
            foreach (var party in involved)
            {
                if (party.Item3 != 0 && Sobriety.Impaired(party.Item3)) { fault = party; why = "the driver showed signs of impairment"; break; }
                if (party.Item2 != null && (party.Item2.Licence == "suspended" || party.Item2.Licence == "unlicensed")) { fault = party; why = "the driver's licence status and the damage pattern"; }
            }

            _chat.Me("You", "walk the scene - the vehicles, the debris field, the skid marks.");
            var draft = new System.Text.StringBuilder("I responded to a traffic collision" + (_dispatch.ReportCallName != null ? " (" + _dispatch.ReportCallName + ")" : "") + ". ");
            draft.Append(involved.Count).Append(" vehicles were involved: ");
            for (var i = 0; i < involved.Count; i++)
            {
                var party = involved[i];
                var damage = party.Item1.Health >= 900 ? "minor damage" : party.Item1.Health >= 600 ? "moderate damage" : "heavy damage";
                var statement = party.Item2 == null ? "no driver present"
                              : Statement(party.Item3, party == fault, party.Item2);
                _chat.Do("Vehicle " + (i + 1) + ": " + party.Item4 + ", " + damage + (party.Item2 != null ? ", driven by " + party.Item2.Name : ", nobody with it") + ".");
                if (party.Item2 != null) _chat.Local(party.Item2.Name + ":", statement);
                draft.Append("Vehicle ").Append(i + 1).Append(", ").Append(party.Item4).Append(", ").Append(damage)
                     .Append(party.Item2 != null ? ", driven by " + party.Item2.Name + ", who stated: \"" + statement + "\"" : "").Append(". ");
                if (party.Item2 != null) _records.Act(party.Item2, _dispatch.ReportCallName, "took their statement at a collision involving " + party.Item4);
            }
            if (involved.Count > 1 || fault.Item2 != null)
            {
                draft.Append("Based on ").Append(why).Append(", I determined vehicle ").Append(involved.IndexOf(fault) + 1)
                     .Append(fault.Item2 != null ? " (" + fault.Item2.Name + ")" : "").Append(" to be at fault.");
                _chat.Notice("  At fault: vehicle " + (involved.IndexOf(fault) + 1) + (fault.Item2 != null ? ", " + fault.Item2.Name : "") + " - " + why + ".");
            }
            _records.IncidentNote("collision report: " + involved.Count + " vehicle(s)");

            _chat.IsOpen = true;
            _chat.Input = "/report " + (fault.Item2 != null ? fault.Item2.Name + ": " : "general: ") + draft.ToString().Trim();
            _chat.Scroll = 0;
            _chat.Notice("Collision report drafted in the box - Enter files it. '/cite <name> careless driving' for the driver at fault.");
        }

        private static string Statement(int handle, bool atFault, PersonRecord person)
        {
            var rng = new Random(unchecked(handle * 41 + 7));
            if (handle != 0 && Sobriety.Impaired(handle))
                return new[] { "Came outta nowhere, man. Nowhere.", "I was going slow, I swear... the light was green. Or yellow.", "I'm fine. I'm totally fine. He hit me." }[rng.Next(3)];
            if (atFault)
                return new[] { "I looked down for one second and they'd stopped.", "I thought I had time to make the turn.", "The sun was in my eyes, I didn't see them." }[rng.Next(3)];
            return new[] { "I was stopped at the light and they slammed into the back of me.", "They pulled out right in front of me - I had nowhere to go.", "I had the green. They just ran it." }[rng.Next(3)];
        }

        // ------------------------------------------------------------------ the checks, together

        /// <summary>
        /// /checks: everything about the car you stopped in one go - the plate on file, the driver's licence
        /// and warrants from LSPDFR, and the insurance and registration checks of whichever plugin owns them.
        /// </summary>
        private void Checks()
        {
            var vehicle = _api.PulloverVehicle() ?? _api.NearestOtherVehicle(10f);
            if (vehicle == null) { _chat.Error("No stopped vehicle - make a stop, or stand by the car."); return; }

            _chat.Me("You", "run the plate, the driver and the paperwork.");
            string plate = null, model = null;
            try { plate = vehicle.LicensePlate.Trim(); model = vehicle.Model.Name; } catch { }
            if (!string.IsNullOrEmpty(plate)) PlateLookup(plate);

            Ped driver = _api.PulloverSuspect();
            try { if (driver == null) driver = vehicle.Driver; } catch { }
            if (driver != null && driver.Exists())
            {
                var handle = unchecked((int)driver.Handle.Value);
                var person = _records.EnsurePerson(handle, Identities.NameFor(handle));
                _records.Touch(person);
                PersonaInfo.Sync(_api, driver, _records, person);
                _chat.Notice("Driver: " + person.Name + ", " + person.Age + " - licence " + (person.Licence ?? "not on the state system").ToUpperInvariant() +
                             (person.Points > 0 ? " (" + person.Points + " pts)" : "") +
                             " - " + (person.Wanted ? "WANTED: " + person.WarrantFor : "no warrants") +
                             (person.Priors.Count > 0 ? " - " + person.Priors.Count + " prior(s)" : ""));
                ShowFlags(person.Name);
            }

            // The plugins that own insurance and registration answer for themselves, when installed.
            BridgeCheck("insurance");
            BridgeCheck("registration");

            var locked = LockFor(vehicle);
            if (locked != null) _chat.Notice("Radar: locked at " + locked.Mph + " mph, " + locked.At.ToString("HH:mm") + ".");
        }

        /// <summary>/passengers: who else is in the car you stopped, with names and what is on file - and /id passenger.</summary>
        private void Passengers(bool identify)
        {
            var vehicle = _api.PulloverVehicle() ?? _api.NearestOtherVehicle(10f);
            if (vehicle == null) { _chat.Error("No vehicle close enough."); return; }

            Ped[] occupants = null;
            try { occupants = vehicle.Occupants; } catch { }
            Ped driver = null;
            try { driver = vehicle.Driver; } catch { }

            var found = 0;
            if (occupants != null)
                foreach (var ped in occupants)
                {
                    if (ped == null || !ped.Exists() || ped == driver) continue;
                    found++;
                    var handle = unchecked((int)ped.Handle.Value);
                    var person = _records.EnsurePerson(handle, Identities.NameFor(handle));
                    if (identify)
                    {
                        _records.Touch(person);
                        PersonaInfo.Sync(_api, ped, _records, person);
                        _api.DisplayPedId(ped);
                        _chat.Me("You", "ask the passenger for their ID.");
                        _chat.Do(person.Name + ", " + person.Age + " - " + (person.Wanted ? "WANTED: " + person.WarrantFor : person.Summary()) + ".");
                    }
                    else _chat.Notice("  Passenger " + found + ": " + (identify ? person.Name : "not yet identified") + " - " + Identities.MoodFor(handle).ToString().ToLowerInvariant());
                }

            if (found == 0) _chat.Notice("Nobody in the car but the driver.");
            else if (!identify) _chat.Notice("  '/id passenger' asks them for ID.");
        }
    }
}
