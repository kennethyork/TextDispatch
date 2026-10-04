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

        public readonly List<string> Priors = new List<string>();

        public bool Wanted;
        public string WarrantFor;

        public double UnpaidFines;
        public int Arrests;

        public string Summary()
        {
            if (Wanted && Priors.Count > 0) return "wanted, with priors";
            if (Wanted) return "outstanding warrant";
            if (Priors.Count > 0) return Priors.Count + " prior(s)";
            return "no record";
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
    }

    public sealed class BoloRecord
    {
        public string Subject;
        public string Reason;
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

        public int CitationCount { get; private set; }
        public int ArrestCount { get; private set; }
        public double FinesIssued { get; private set; }

        public ICollection<PersonRecord> People { get { return _people.Values; } }
        public ICollection<VehicleRecord> Vehicles { get { return _vehicles.Values; } }
        public List<BoloRecord> Bolos { get { return _bolos; } }

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
            return record;
        }

        public void AddBolo(string subject, string reason)
        {
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
                return true;
            }
            return false;
        }

        public string IssueCitation(PersonRecord person, string offence, double fine)
        {
            person.UnpaidFines += fine;
            CitationCount++;
            FinesIssued += fine;

            return "citation issued - " + person.Name + " - " + offence + " - $" + fine.ToString("0.00");
        }

        public string RecordArrest(PersonRecord person, string offence)
        {
            person.Arrests++;
            if (!string.IsNullOrEmpty(offence)) person.Priors.Add(offence);

            // An arrest settles what the warrant was for.
            var hadWarrant = person.Wanted;
            person.Wanted = false;
            person.WarrantFor = null;

            ArrestCount++;

            return "arrest recorded - " + person.Name + " - " + offence +
                   (hadWarrant ? " (warrant cleared)" : "");
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
