using System;
using System.Reflection;
using Rage;
using TextDispatch.Records;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// What LSPDFR itself says about a person: the identity its own ID card shows, their licence and
    /// whether it wants them. Read by reflection, so a property one LSPDFR build renames costs only that
    /// field, never the plugin.
    ///
    /// This is what makes the box and LSPDFR agree. Before, TextDispatch gave a stopped driver a name of
    /// its own while LSPDFR's ID check showed another; now the person in the records is the person on the
    /// licence, and their licence and warrant status are LSPDFR's.
    /// </summary>
    internal sealed class PersonaInfo
    {
        public string Name;
        public string Gender;
        public DateTime? Birthday;
        public string Licence;     // valid, expired, suspended, unlicensed - or null when LSPDFR did not say
        public bool Wanted;
        public int TimesStopped = -1;

        public int Age
        {
            get
            {
                if (Birthday == null) return 0;
                var now = DateTime.Now;
                var age = now.Year - Birthday.Value.Year;
                if (now < Birthday.Value.AddYears(age)) age--;
                return age > 0 && age < 120 ? age : 0;
            }
        }

        public static PersonaInfo For(LspdfrApi api, Ped ped)
        {
            if (api == null || ped == null) return null;
            try
            {
                if (!ped.Exists()) return null;
                return Read(api.PersonaObject(ped));
            }
            catch { return null; }
        }

        public static PersonaInfo Read(object persona)
        {
            if (persona == null) return null;
            var info = new PersonaInfo();

            info.Name = Text(persona, "FullName");
            if (string.IsNullOrWhiteSpace(info.Name))
            {
                var first = Text(persona, "Forename") ?? Text(persona, "FirstName");
                var last = Text(persona, "Surname") ?? Text(persona, "LastName");
                info.Name = (first + " " + last).Trim();
            }
            if (string.IsNullOrWhiteSpace(info.Name)) info.Name = null;

            info.Gender = Text(persona, "Gender");
            var birthday = Value(persona, "Birthday") ?? Value(persona, "BirthDay");
            if (birthday is DateTime) info.Birthday = (DateTime)birthday;

            var licence = Text(persona, "ELicenseState") ?? Text(persona, "LicenseState") ?? Text(persona, "ELicenceState");
            if (licence != null)
            {
                var l = licence.ToLowerInvariant();
                info.Licence = l.Contains("susp") ? "suspended" : l.Contains("expir") ? "expired" : l.Contains("unlic") || l.Contains("none") ? "unlicensed" : "valid";
            }

            var wanted = Value(persona, "Wanted") ?? Value(persona, "IsWanted");
            info.Wanted = wanted is bool && (bool)wanted;

            var stopped = Value(persona, "TimesStopped");
            if (stopped is int) info.TimesStopped = (int)stopped;
            return info;
        }

        /// <summary>
        /// Put what LSPDFR knows onto the person on file: the age from the birthday, the licence, and a
        /// warrant when LSPDFR has one and the file does not. Never takes a warrant away - an LSPDFR "not
        /// wanted" says nothing about a warrant the courts issued in TextDispatch.
        /// </summary>
        public void ApplyTo(RecordsLedger ledger, PersonRecord person)
        {
            if (person == null) return;
            var changed = false;
            if (Age > 0 && person.Age != Age) { person.Age = Age; changed = true; }
            if (Licence != null && person.Licence != Licence) { person.Licence = Licence; changed = true; }
            if (Wanted && !person.Wanted)
            {
                person.Wanted = true;
                person.WarrantFor = "an outstanding warrant on the state system";
                changed = true;
            }
            if (TimesStopped >= 0 && person.TimesStopped != TimesStopped) { person.TimesStopped = TimesStopped; changed = true; }
            if (changed && ledger != null) ledger.Dirty = true;
        }

        /// <summary>Read LSPDFR's persona for a ped and put it on their record. Quiet when LSPDFR has none.</summary>
        public static PersonaInfo Sync(LspdfrApi api, Ped ped, RecordsLedger ledger, PersonRecord person)
        {
            var info = For(api, ped);
            if (info != null) info.ApplyTo(ledger, person);
            return info;
        }

        private static object Value(object target, string name)
        {
            try
            {
                var type = target.GetType();
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property != null) return property.GetValue(target, null);
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                return field == null ? null : field.GetValue(target);
            }
            catch { return null; }
        }

        private static string Text(object target, string name)
        {
            var value = Value(target, name);
            return value == null ? null : value.ToString();
        }
    }
}
