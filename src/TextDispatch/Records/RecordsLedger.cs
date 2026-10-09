using System;
using System.Collections.Generic;

namespace TextDispatch.Records
{
    /// <summary>A person on file: who they are, what they have done, and what is outstanding.</summary>
    public sealed class PersonRecord
    {
        public string Id;
        public string Name;
        public int Age;
        public string Occupation;
        public string HomeZone;

        // Plain fields rather than readonly ones: the save file has to be able to put them back.
        public List<string> Priors = new List<string>();

        /// <summary>What happened afterwards - court outcomes, fines paid - newest last.</summary>
        public List<string> Notes = new List<string>();

        public bool Wanted;
        public string WarrantFor;

        public double UnpaidFines;
        public int Arrests;
        public int Citations;

        /// <summary>Whether the player has dealt with them, as opposed to being one of the seeded town.</summary>
        public bool Met;

        /// <summary>
        /// Evidence and a statement gathered before there is a case to put them on. An arrest moves
        /// them onto its follow-up; until then they wait here.
        /// </summary>
        public List<string> Evidence = new List<string>();
        public string Statement;
        public bool StatementMirandized;

        /// <summary>When they were read their rights, as DateTime.Now ticks; 0 if never.</summary>
        public long MirandizedTicks;

        /// <summary>Whether they have been read their rights in the last hour - the same contact.</summary>
        [System.Web.Script.Serialization.ScriptIgnore]
        public bool Mirandized
        {
            get { return MirandizedTicks != 0 && DateTime.Now.Ticks - MirandizedTicks < TimeSpan.FromHours(1).Ticks; }
        }

        public string Summary()
        {
            if (Wanted && Priors.Count > 0) return "wanted, with priors";
            if (Wanted) return "outstanding warrant";
            if (Priors.Count > 0) return Priors.Count + " prior(s)";
            return "no record";
        }

        /// <summary>
        /// One line describing who this person is, for the conversation prompt.
        ///
        /// The point of putting it here rather than in the dialogue code is that the records terminal
        /// and the conversation describe the same person from the same source - the officer asking
        /// "who are you" and the terminal saying "34, a mechanic, from Del Perro" are one system, not two.
        /// </summary>
        public string CharacterLine()
        {
            var line = Age + ", " + (string.IsNullOrEmpty(Occupation) ? "no job on record" : "a " + Occupation.ToLowerInvariant());
            if (!string.IsNullOrEmpty(HomeZone)) line += ", lives in " + HomeZone;

            if (Wanted) line += ". You know the police are looking for you over " + WarrantFor;
            else if (Priors.Count > 0) line += ". You have been in trouble before and do not want more of it";

            return line + ".";
        }
    }

    /// <summary>A vehicle on file. The owner link is what makes a plate check worth running.</summary>
    public sealed class VehicleRecord
    {
        public string Plate;
        public string Model;
        public string OwnerName;
        public bool Insured = true;
        public bool ReportedStolen;

        /// <summary>
        /// What a search of it turned up, decided the first time it is searched and kept, so the
        /// same car searched twice has the same thing in it. Null until it has been searched.
        /// </summary>
        public string SearchFind;
        public bool SearchIllegal;
    }

    public sealed class BoloRecord
    {
        public string Subject;
        public string Reason;
    }

    /// <summary>A written report: what the officer said happened, on whom, on which call.</summary>
    public sealed class ReportRecord
    {
        public int Number;
        public string When;      // local time, "yyyy-MM-dd HH:mm"
        public string Unit;
        public string Call;      // the call it was written on, or null
        public string Subject;   // the person it is about, or null for a general report
        public string Text;
    }

    /// <summary>
    /// Something that comes back on the radio later: what the court did with an arrest, whether a
    /// citation was paid. Due at a real time, so one left pending when the game closes is delivered
    /// at the start of the next session.
    /// </summary>
    public sealed class FollowUp
    {
        public string PersonId;
        public string Name;
        public string Offence;
        public string Kind;      // "arrest" or "citation"
        public double Fine;
        public string Call;
        public string Made;      // local time it was made, "yyyy-MM-dd HH:mm"
        public long DueTicks;    // DateTime.UtcNow.Ticks it is due at
        public bool ReportFiled;

        /// <summary>What the case has behind it besides the report.</summary>
        public List<string> Evidence = new List<string>();
        public string Statement;     // confession | admission | lawyer | denial, or null
        public bool Mirandized;      // whether the statement was taken after the rights were read
    }

    /// <summary>What an interview got: the kind of statement, the words, and whether it will stand up.</summary>
    public sealed class Interview
    {
        public string Kind;
        public string Line;
        public bool Mirandized;
    }

    /// <summary>
    /// One thing the officer did, kept so a report can be drafted from what actually happened rather
    /// than from memory.
    /// </summary>
    public sealed class CaseAction
    {
        public long Ticks;       // DateTime.Now.Ticks
        public string PersonId;
        public string Name;
        public string Call;
        public string Text;      // past tense, ready for a report: "searched the vehicle (ABC123)..."
    }

    /// <summary>One shift, as it was summed up when it ended.</summary>
    public sealed class ShiftSummary
    {
        public string Unit;
        public string Started;   // local time
        public string Ended;     // local time
        public int Minutes;
        public List<string> Calls = new List<string>();
        public int Stops;
        public int Pursuits;
        public int Arrests;
        public int Citations;
        public double Fines;
        public int Searches;
        public int Finds;
        public int Reports;
        public int Backups;
        public bool Unfinished;  // the game closed before the shift was ended
    }

    /// <summary>
    /// The records terminal's data.
    ///
    /// GTA World's MDT is a server database, which is exactly why this is the one part of that server's
    /// command set that can be rebuilt honestly in singleplayer: a database needs no other players.
    ///
    /// It is seeded deterministically, so the town is the same town every session - you learn who is
    /// worth running and who is not - and it has no reference to the game at all, which keeps it
    /// testable outside it.
    /// </summary>
    public sealed class RecordsLedger
    {
        private readonly Dictionary<string, PersonRecord> _people = new Dictionary<string, PersonRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, VehicleRecord> _vehicles = new Dictionary<string, VehicleRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BoloRecord> _bolos = new List<BoloRecord>();

        /// <summary>Maps a game entity handle to the record it was given, so a person stays the same person.</summary>
        private readonly Dictionary<int, string> _byHandle = new Dictionary<int, string>();

        private readonly List<ReportRecord> _reports = new List<ReportRecord>();
        private readonly List<FollowUp> _followUps = new List<FollowUp>();
        private readonly List<ShiftSummary> _shifts = new List<ShiftSummary>();
        private readonly List<CaseAction> _actions = new List<CaseAction>();

        public int CitationCount { get; internal set; }
        public int ArrestCount { get; internal set; }
        public double FinesIssued { get; internal set; }

        /// <summary>The next report number. Starts somewhere a records office would plausibly be.</summary>
        public int NextReport { get; internal set; } = 1001;

        /// <summary>The player's callsign, kept from one session to the next once it has been given.</summary>
        public string Unit { get; internal set; }

        /// <summary>
        /// Set by anything that changes what is on file, and cleared by the store when it has written
        /// it. Saving on a flag rather than on every change keeps the disk out of the tick.
        /// </summary>
        public bool Dirty;

        /// <summary>Who the player last dealt with by name - the person a /report is about by default.</summary>
        public string LastSubjectId;

        public ICollection<PersonRecord> People { get { return _people.Values; } }
        public ICollection<VehicleRecord> Vehicles { get { return _vehicles.Values; } }
        public List<BoloRecord> Bolos { get { return _bolos; } }
        public List<ReportRecord> Reports { get { return _reports; } }
        public List<FollowUp> FollowUps { get { return _followUps; } }
        public List<ShiftSummary> Shifts { get { return _shifts; } }
        public List<CaseAction> Actions { get { return _actions; } }

        public PersonRecord LastSubject
        {
            get
            {
                PersonRecord person;
                return LastSubjectId != null && _people.TryGetValue(LastSubjectId, out person) ? person : null;
            }
        }

        /// <summary>The player has dealt with this person: remember them, and make them the default subject.</summary>
        public void Touch(PersonRecord person)
        {
            if (person == null) return;
            person.Met = true;
            LastSubjectId = person.Id;
            Dirty = true;
        }

        /// <summary>Put a saved record back, replacing the seeded one of the same name.</summary>
        internal void Restore(PersonRecord person)
        {
            if (person == null || string.IsNullOrEmpty(person.Name)) return;
            if (string.IsNullOrEmpty(person.Id)) person.Id = Key(person.Name);
            if (person.Priors == null) person.Priors = new List<string>();
            if (person.Notes == null) person.Notes = new List<string>();
            if (person.Evidence == null) person.Evidence = new List<string>();
            _people[person.Id] = person;
        }

        internal void Restore(VehicleRecord vehicle)
        {
            if (vehicle == null || string.IsNullOrEmpty(vehicle.Plate)) return;
            vehicle.Plate = PlateKey(vehicle.Plate);
            _vehicles[vehicle.Plate] = vehicle;
        }

        // ------------------------------------------------------------------ lookup

        public static string Key(string text)
        {
            return (text ?? "").Trim().ToLowerInvariant();
        }

        public static string PlateKey(string plate)
        {
            return (plate ?? "").Trim().ToUpperInvariant().Replace(" ", "");
        }

        public PersonRecord FindPerson(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            PersonRecord found;
            if (_people.TryGetValue(Key(name), out found)) return found;

            // Partial match, so "Vasquez" finds "Marisol Vasquez" - a dispatcher would not need the
            // whole name either.
            var needle = Key(name);
            foreach (var person in _people.Values)
                if (person.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return person;

            return null;
        }

        public VehicleRecord FindVehicle(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) return null;

            VehicleRecord found;
            return _vehicles.TryGetValue(PlateKey(plate), out found) ? found : null;
        }

        public PersonRecord PersonForHandle(int handle)
        {
            string id;
            if (!_byHandle.TryGetValue(handle, out id)) return null;

            PersonRecord person;
            return _people.TryGetValue(id, out person) ? person : null;
        }

        // ------------------------------------------------------------------ filing

        /// <summary>This game entity is this person - for somebody spawned to be a particular record.</summary>
        public void Bind(int handle, PersonRecord person)
        {
            if (person == null) return;
            _byHandle[handle] = person.Id;
        }

        public PersonRecord EnsurePerson(int handle, string name)
        {
            var existing = PersonForHandle(handle);
            if (existing != null) return existing;

            var person = FindPerson(name);
            if (person == null)
            {
                person = new PersonRecord { Id = Key(name), Name = name };
                Describe(person, new Random(unchecked(handle * 31 + 7)));
                _people[person.Id] = person;
                Dirty = true;
            }

            _byHandle[handle] = person.Id;
            return person;
        }

        public VehicleRecord EnsureVehicle(string plate, string model)
        {
            var key = PlateKey(plate);
            if (key.Length == 0) return null;

            VehicleRecord record;
            if (_vehicles.TryGetValue(key, out record)) return record;

            var rng = new Random(key.GetHashCode());
            var owners = new List<PersonRecord>(_people.Values);
            var owner = owners.Count > 0 ? owners[rng.Next(owners.Count)] : null;

            record = new VehicleRecord
            {
                Plate = key,
                Model = string.IsNullOrEmpty(model) ? "Unknown model" : model,
                OwnerName = owner != null ? owner.Name : "unknown",
                Insured = rng.Next(100) >= 12,
                ReportedStolen = rng.Next(100) < 7
            };

            _vehicles[key] = record;
            Dirty = true;
            return record;
        }

        public void AddBolo(string subject, string reason)
        {
            Dirty = true;
            foreach (var bolo in _bolos)
            {
                if (!string.Equals(bolo.Subject, subject, StringComparison.OrdinalIgnoreCase)) continue;
                bolo.Reason = reason;
                return;
            }

            _bolos.Add(new BoloRecord { Subject = subject, Reason = reason });
        }

        public bool ClearBolo(string subject)
        {
            for (int i = 0; i < _bolos.Count; i++)
            {
                if (!string.Equals(_bolos[i].Subject, subject, StringComparison.OrdinalIgnoreCase)) continue;
                _bolos.RemoveAt(i);
                Dirty = true;
                return true;
            }
            return false;
        }

        public string IssueCitation(PersonRecord person, string offence, double fine, string call = null)
        {
            person.UnpaidFines += fine;
            person.Citations++;
            CitationCount++;
            FinesIssued += fine;
            Touch(person);
            Act(person, call, "cited them for " + offence + " ($" + fine.ToString("0") + ")");

            return "citation issued - " + person.Name + " - " + offence + " - $" + fine.ToString("0.00");
        }

        public string RecordArrest(PersonRecord person, string offence, string call = null)
        {
            var onWarrant = person.Wanted && string.Equals(offence, person.WarrantFor, StringComparison.OrdinalIgnoreCase);
            Act(person, call, onWarrant ? "arrested them on the outstanding warrant for " + offence : "arrested them for " + offence);

            person.Arrests++;
            if (!string.IsNullOrEmpty(offence)) person.Priors.Add(offence);

            // An arrest settles what the warrant was for.
            var hadWarrant = person.Wanted;
            person.Wanted = false;
            person.WarrantFor = null;

            ArrestCount++;
            Touch(person);

            return "arrest recorded - " + person.Name + " - " + offence +
                   (hadWarrant ? " (warrant cleared)" : "");
        }

        // ------------------------------------------------------------------ vehicle searches

        private static readonly string[] IllegalFinds =
        {
            "a bag of white powder that looks like cocaine, tucked in the door pocket",
            "an unregistered pistol under the driver's seat",
            "a bag of pills with no prescription label",
            "a small bag of cannabis and a scale",
            "a set of lock picks, a slim jim and three phones",
            "$4,000 in cash wrapped in rubber bands",
            "an open bottle of vodka, half gone, in the footwell",
            "a sawn-off shotgun wrapped in a towel in the trunk",
            "a stack of credit cards in other people's names",
            "a crowbar and a bag of car stereos"
        };

        private static readonly string[] LegalFinds =
        {
            "fast food wrappers and an empty coffee cup",
            "a gym bag, jumper cables and a spare tyre",
            "work tools and a hard hat",
            "a child seat and some school books",
            "groceries in the trunk",
            "a laptop bag and some paperwork",
            "fishing gear and a cooler",
            "nothing but the owner's manual and a phone charger"
        };

        /// <summary>
        /// What a search of this vehicle finds. Decided once, from the plate, and kept on the record -
        /// so it is the same car with the same thing in it however often it is searched, and the
        /// chance of something illegal follows what is on file: a stolen car, or a driver with a
        /// warrant or a history, is likelier to be carrying something.
        /// </summary>
        public VehicleRecord SearchVehicle(string plate, string model, PersonRecord driver, string call = null)
        {
            var vehicle = EnsureVehicle(plate, model);
            if (vehicle == null) return null;
            if (vehicle.SearchFind != null) return vehicle;

            vehicle = DecideSearch(vehicle, plate, driver);

            if (driver != null)
            {
                Act(driver, call, "searched the vehicle (" + vehicle.Plate + ") and found " + vehicle.SearchFind);
                if (vehicle.SearchIllegal) AddEvidence(driver, vehicle.SearchFind + ", found in vehicle " + vehicle.Plate, call, false);
            }
            return vehicle;
        }

        private VehicleRecord DecideSearch(VehicleRecord vehicle, string plate, PersonRecord driver)
        {

            var rng = new Random(unchecked(PlateKey(plate).GetHashCode() * 17 + 3));

            var chance = 18;
            if (vehicle.ReportedStolen) chance += 35;
            var owner = driver ?? FindPerson(vehicle.OwnerName);
            if (owner != null && owner.Wanted) chance += 30;
            if (owner != null && owner.Priors.Count > 0) chance += 20;

            vehicle.SearchIllegal = rng.Next(100) < chance;
            vehicle.SearchFind = vehicle.SearchIllegal
                ? IllegalFinds[rng.Next(IllegalFinds.Length)]
                : LegalFinds[rng.Next(LegalFinds.Length)];

            if (driver != null) Touch(driver);
            Dirty = true;
            return vehicle;
        }

        // ------------------------------------------------------------------ the case: what was done, and what it rests on

        /// <summary>Write down something the officer did, for a report to be drafted from.</summary>
        public void Act(PersonRecord person, string call, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _actions.Add(new CaseAction
            {
                Ticks = DateTime.Now.Ticks,
                PersonId = person == null ? null : person.Id,
                Name = person == null ? null : person.Name,
                Call = string.IsNullOrEmpty(call) ? null : call,
                Text = text
            });
            if (_actions.Count > 200) _actions.RemoveAt(0);
            Dirty = true;
        }

        /// <summary>The arrest still waiting on the courts for this person, if there is one.</summary>
        public FollowUp OpenCase(PersonRecord person)
        {
            if (person == null) return null;
            foreach (var followUp in _followUps)
                if (followUp.PersonId == person.Id && followUp.Kind == "arrest") return followUp;
            return null;
        }

        /// <summary>
        /// Put evidence against someone. On the arrest waiting for the courts if there is one - evidence
        /// logged after the cuffs are on still counts - and held on the person until an arrest otherwise.
        /// </summary>
        public void AddEvidence(PersonRecord person, string item, string call, bool logAction = true)
        {
            if (person == null || string.IsNullOrWhiteSpace(item)) return;
            item = item.Trim();

            var open = OpenCase(person);
            var list = open != null ? open.Evidence : person.Evidence;
            if (!list.Contains(item)) list.Add(item);

            if (logAction) Act(person, call, "logged into evidence: " + item);
            Touch(person);
        }

        /// <summary>Everything against them, on the open case and waiting on the person.</summary>
        public List<string> EvidenceOn(PersonRecord person)
        {
            var all = new List<string>();
            if (person == null) return all;
            var open = OpenCase(person);
            if (open != null) all.AddRange(open.Evidence);
            all.AddRange(person.Evidence);
            return all;
        }

        public void ReadRights(PersonRecord person, string call)
        {
            if (person == null) return;
            person.MirandizedTicks = DateTime.Now.Ticks;
            Act(person, call, "read them their Miranda rights");
            Touch(person);
        }

        /// <summary>
        /// Question them about the offence. What they say follows their temperament (0 compliant,
        /// 1 nervous, 2 defensive, 3 hostile) and how much evidence they are faced with, and it is kept
        /// on the case - but a statement taken without the rights read is worthless in court, which
        /// the result says.
        /// </summary>
        public Interview Interview(PersonRecord person, int temperament, string call)
        {
            if (person == null) return null;

            var evidence = EvidenceOn(person).Count;
            var rng = new Random(unchecked(person.Id.GetHashCode() * 7 + (int)(DateTime.Now.Ticks / TimeSpan.TicksPerMinute)));

            // Out of 100: a confession is likelier the more they are shown, and the calmer they are.
            var confess = 15 + 15 * Math.Min(3, evidence) - 8 * temperament;
            var admit = 25;
            var lawyer = 10 + 10 * temperament;
            var roll = rng.Next(100);

            string kind;
            if (roll < confess) kind = "confession";
            else if (roll < confess + admit) kind = "admission";
            else if (roll < confess + admit + lawyer) kind = "lawyer";
            else kind = "denial";

            var result = new Interview
            {
                Kind = kind,
                Mirandized = person.Mirandized,
                Line = Pick(rng, kind)
            };

            var open = OpenCase(person);
            if (open != null) { open.Statement = kind; open.Mirandized = result.Mirandized; }
            else { person.Statement = kind; person.StatementMirandized = result.Mirandized; }

            Act(person, call, "interviewed them" + (result.Mirandized ? " after reading their rights" : " without reading their rights") +
                              " - " + Describe(kind));
            Touch(person);
            return result;
        }

        private static string Pick(Random rng, string kind)
        {
            string[] lines;
            switch (kind)
            {
                case "confession":
                    lines = new[] { "Alright. Alright, it was me. I did it.", "Fine. Yes. I did it - I'm not going to keep lying to you.", "Okay... it's mine. All of it. I'm sorry." };
                    break;
                case "admission":
                    lines = new[] { "I was there, okay? But it's not what you think.", "Maybe I had something to do with it. Maybe.", "I knew about it. I didn't do the worst of it." };
                    break;
                case "lawyer":
                    lines = new[] { "I want a lawyer. I'm not saying anything else.", "Lawyer. Now.", "I know my rights. I want to speak to an attorney." };
                    break;
                default:
                    lines = new[] { "I didn't do anything. You've got the wrong person.", "That's not mine and I wasn't there.", "No. No way. I want to go home." };
                    break;
            }
            return lines[rng.Next(lines.Length)];
        }

        public static string Describe(string statement)
        {
            switch (statement)
            {
                case "confession": return "they confessed";
                case "admission": return "they admitted being involved";
                case "lawyer": return "they asked for a lawyer";
                case "denial": return "they denied it";
                default: return "no statement";
            }
        }

        /// <summary>
        /// The chance in a hundred that an arrest is charged: the report, the evidence and the statement
        /// each add to it. A statement only counts if it was taken after the rights were read.
        /// </summary>
        public static int ChargeChance(FollowUp followUp)
        {
            var chance = 40;
            if (followUp.ReportFiled) chance += 30;
            chance += Math.Min(30, 12 * (followUp.Evidence == null ? 0 : followUp.Evidence.Count));

            if (followUp.Mirandized)
            {
                switch (followUp.Statement)
                {
                    case "confession": chance += 25; break;
                    case "admission": chance += 12; break;
                    case "denial": chance -= 5; break;
                }
            }

            return Math.Max(5, Math.Min(97, chance));
        }

        /// <summary>People with a warrant the player already knows - the ones a warrant service call is for.</summary>
        public List<PersonRecord> KnownWanted()
        {
            var found = new List<PersonRecord>();
            foreach (var person in _people.Values)
                if (person.Met && person.Wanted) found.Add(person);
            return found;
        }

        // ------------------------------------------------------------------ reports

        /// <summary>File a report. Any follow-up waiting on that person now has it to go on.</summary>
        public ReportRecord FileReport(string unit, string call, PersonRecord subject, string text)
        {
            var report = new ReportRecord
            {
                Number = NextReport++,
                When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Unit = unit,
                Call = string.IsNullOrEmpty(call) ? null : call,
                Subject = subject == null ? null : subject.Name,
                Text = (text ?? "").Trim()
            };

            _reports.Add(report);
            if (_reports.Count > 500) _reports.RemoveAt(0);

            if (subject != null)
            {
                foreach (var followUp in _followUps)
                    if (followUp.PersonId == subject.Id) followUp.ReportFiled = true;
                Touch(subject);
            }

            Dirty = true;
            return report;
        }

        public List<ReportRecord> ReportsAbout(PersonRecord person)
        {
            var found = new List<ReportRecord>();
            if (person == null) return found;
            foreach (var report in _reports)
                if (string.Equals(report.Subject, person.Name, StringComparison.OrdinalIgnoreCase)) found.Add(report);
            return found;
        }

        // ------------------------------------------------------------------ follow-ups

        /// <summary>
        /// Something that will come back on the radio later. Three to eight minutes, so it arrives
        /// on the same shift as a rule, and a report written in the meantime still counts.
        /// </summary>
        public FollowUp ScheduleFollowUp(PersonRecord person, string kind, string offence, double fine, string call)
        {
            if (person == null) return null;

            var minutes = 3 + new Random(unchecked(person.Id.GetHashCode() + Environment.TickCount)).Next(6);
            var followUp = new FollowUp
            {
                PersonId = person.Id,
                Name = person.Name,
                Offence = string.IsNullOrEmpty(offence) ? "the offence" : offence,
                Kind = kind,
                Fine = fine,
                Call = call,
                Made = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                DueTicks = DateTime.UtcNow.AddMinutes(minutes).Ticks
            };

            foreach (var report in _reports)
                if (string.Equals(report.Subject, person.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(report.Call ?? "", call ?? "", StringComparison.OrdinalIgnoreCase))
                    followUp.ReportFiled = true;

            // What was gathered before the arrest goes onto it: the evidence, and what they said.
            if (kind == "arrest")
            {
                followUp.Evidence.AddRange(person.Evidence);
                person.Evidence.Clear();
                followUp.Statement = person.Statement;
                followUp.Mirandized = person.StatementMirandized;
                person.Statement = null;
                person.StatementMirandized = false;
            }

            _followUps.Add(followUp);
            Dirty = true;
            return followUp;
        }

        /// <summary>The first follow-up that is due, or null.</summary>
        public FollowUp DueFollowUp(DateTime utcNow)
        {
            foreach (var followUp in _followUps)
                if (followUp.DueTicks <= utcNow.Ticks) return followUp;
            return null;
        }

        /// <summary>
        /// Decide what happened, put it on the person's record, and say it. An arrest with a report
        /// behind it is far likelier to be charged - which is the reason to write one.
        /// </summary>
        public string Resolve(FollowUp followUp)
        {
            _followUps.Remove(followUp);
            Dirty = true;

            PersonRecord person;
            _people.TryGetValue(followUp.PersonId ?? "", out person);

            var rng = new Random(unchecked((followUp.PersonId ?? "").GetHashCode() * 13 + (int)(followUp.DueTicks % 100000)));
            var roll = rng.Next(100);
            string outcome, note;

            if (followUp.Kind == "citation")
            {
                var fine = "$" + followUp.Fine.ToString("0.00");
                if (roll < 60)
                {
                    outcome = followUp.Name + " has paid the " + fine + " citation for " + followUp.Offence + ".";
                    note = "paid a " + fine + " citation for " + followUp.Offence;
                    if (person != null) person.UnpaidFines = Math.Max(0, person.UnpaidFines - followUp.Fine);
                }
                else if (roll < 80)
                {
                    outcome = followUp.Name + " contested the citation for " + followUp.Offence + ". The court upheld it.";
                    note = "contested a citation for " + followUp.Offence + " - upheld";
                }
                else
                {
                    outcome = followUp.Name + " failed to pay the citation for " + followUp.Offence +
                              ". A warrant has been issued for failure to pay.";
                    note = "failed to pay a citation for " + followUp.Offence + " - warrant issued";
                    if (person != null) { person.Wanted = true; person.WarrantFor = "failure to pay a citation"; }
                }
            }
            else
            {
                var charged = roll < ChargeChance(followUp);
                if (followUp.Evidence == null) followUp.Evidence = new List<string>();
                var strength = followUp.Mirandized && followUp.Statement == "confession" ? " on the strength of the confession"
                             : followUp.Evidence.Count > 0 ? " on the evidence"
                             : "";
                if (charged)
                {
                    var sentence = rng.Next(3);
                    if (sentence == 0)
                    {
                        outcome = followUp.Name + " has been charged with " + followUp.Offence + strength + " and is held for arraignment.";
                        note = "charged with " + followUp.Offence;
                    }
                    else if (sentence == 1)
                    {
                        var days = 10 * (1 + rng.Next(9));
                        outcome = followUp.Name + " pleaded guilty to " + followUp.Offence + " - " + days + " days.";
                        note = "pleaded guilty to " + followUp.Offence + ", " + days + " days";
                    }
                    else
                    {
                        outcome = followUp.Name + " was charged with " + followUp.Offence + strength + " and released on bail pending trial.";
                        note = "charged with " + followUp.Offence + ", on bail";
                    }
                }
                else
                {
                    var why = !followUp.ReportFiled ? " - there was no report on file."
                            : followUp.Statement != null && !followUp.Mirandized ? " - the statement was taken without Miranda, and there was not enough without it."
                            : " - not enough evidence.";
                    outcome = "The DA declined to charge " + followUp.Name + " with " + followUp.Offence + why;
                    note = "not charged with " + followUp.Offence;
                    if (person != null) person.Priors.Remove(followUp.Offence);
                }
            }

            if (person != null) person.Notes.Add(DateTime.Now.ToString("yyyy-MM-dd") + ": " + note);
            return outcome;
        }

        // ------------------------------------------------------------------ shifts

        public void AddShift(ShiftSummary shift)
        {
            if (shift == null) return;
            _shifts.Add(shift);
            if (_shifts.Count > 50) _shifts.RemoveAt(0);
            Dirty = true;
        }

        // ------------------------------------------------------------------ population

        private static readonly string[] First =
        {
            "Marcus", "Dwayne", "Tyrone", "Luis", "Hector", "Andre", "Rico", "Anton", "Derek", "Brett",
            "Corey", "Nate", "Owen", "Ruben", "Vince", "Marisol", "Yolanda", "Denise", "Tanisha", "Rosa",
            "Elena", "Kiana", "Simone", "Mandy", "Kirsty", "Trisha", "Nadia", "Priya", "Amara", "Lena"
        };

        private static readonly string[] Last =
        {
            "Reyes", "Vasquez", "Okafor", "Bennett", "Delgado", "Hollis", "Marchetti", "Novak", "Whitlock",
            "Barnes", "Cole", "Fitzgerald", "Rahman", "Silva", "Tran", "Kowalski", "Mackenzie", "Doyle",
            "Farrow", "Ellison", "Greaves", "Ibarra", "Lomax", "Pruitt"
        };

        private static readonly string[] Jobs =
        {
            "Labourer", "Driver", "Shop assistant", "Barista", "Mechanic", "Security guard", "Nurse",
            "Student", "Unemployed", "Warehouse hand", "Taxi driver", "Chef"
        };

        private static readonly string[] Zones =
        {
            "downtown", "vinewood", "vespucci", "del-perro", "la-mesa", "lsia", "sandy-shores", "paleto-bay"
        };

        private static readonly string[] Offences =
        {
            "petty theft", "assault", "drug possession", "vandalism", "resisting arrest", "DUI",
            "unlicensed firearm", "grand theft auto", "fraud", "disorderly conduct"
        };

        private static readonly string[] Models =
        {
            "Bravado Buffalo", "Karin Sultan", "Vapid Stanier", "Declasse Asea", "Albany Primo",
            "Chevalier Fugitive", "Obey Tailgater", "Ubermacht Oracle", "Dundreary Regina", "Zirconium Stratum"
        };

        /// <summary>
        /// Build the town. Fixed seed on purpose: the same population every session, so the person the
        /// dispatcher flags is still the same person tomorrow.
        /// </summary>
        public static RecordsLedger Populate(int seed)
        {
            var ledger = new RecordsLedger();
            var rng = new Random(seed);

            for (int i = 0; i < 36; i++)
            {
                var person = new PersonRecord { Name = First[rng.Next(First.Length)] + " " + Last[rng.Next(Last.Length)] };
                person.Id = Key(person.Name);

                if (ledger._people.ContainsKey(person.Id)) continue;

                Describe(person, rng);
                ledger._people[person.Id] = person;
            }

            var owners = new List<PersonRecord>(ledger._people.Values);

            for (int i = 0; i < 34 && owners.Count > 0; i++)
            {
                var plate = Plate(rng);
                if (ledger._vehicles.ContainsKey(plate)) continue;

                var owner = owners[rng.Next(owners.Count)];
                ledger._vehicles[plate] = new VehicleRecord
                {
                    Plate = plate,
                    Model = Models[rng.Next(Models.Length)],
                    OwnerName = owner.Name,
                    Insured = rng.Next(100) >= 12,
                    ReportedStolen = rng.Next(100) < 7
                };
            }

            return ledger;
        }

        /// <summary>Give a fresh person an age, a job, a home, and sometimes a history.</summary>
        private static void Describe(PersonRecord person, Random rng)
        {
            person.Age = 19 + rng.Next(46);
            person.Occupation = Jobs[rng.Next(Jobs.Length)];
            person.HomeZone = Zones[rng.Next(Zones.Length)];

            if (rng.Next(100) < 22) person.Priors.Add(Offences[rng.Next(Offences.Length)]);

            if (rng.Next(100) < 9)
            {
                person.Wanted = true;
                person.WarrantFor = Offences[rng.Next(Offences.Length)];
            }
        }

        private static string Plate(Random rng)
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";

            var plate = new char[6];
            for (int i = 0; i < 3; i++) plate[i] = letters[rng.Next(letters.Length)];
            for (int i = 3; i < 6; i++) plate[i] = digits[rng.Next(digits.Length)];

            return new string(plate);
        }
    }
}
