using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace TextDispatch.Records
{
    /// <summary>
    /// The records on disk, so a session is not the whole of the world's memory.
    ///
    /// The town is still seeded the same every time; what is saved is what the player has done to it -
    /// the people they have met and booked, the cars they have searched, BOLOs, reports, what is
    /// still to come back from court, the shift history and the callsign. Loaded over the seeded town
    /// at startup, so a record on file replaces the seeded one of the same name.
    ///
    /// One JSON file beside the log. Written to a temporary file and moved into place, with the last
    /// good copy kept as .bak - a save interrupted by the game closing must not cost the whole history.
    /// </summary>
    public static class RecordsStore
    {
        public const int FormatVersion = 1;

        /// <summary>What is written. Plain lists, because that is what the serializer reads back.</summary>
        public sealed class SaveFile
        {
            public int Version;
            public string Saved;
            public string Unit;
            public int CitationCount;
            public int ArrestCount;
            public double FinesIssued;
            public int NextReport;
            public List<PersonRecord> People = new List<PersonRecord>();
            public List<VehicleRecord> Vehicles = new List<VehicleRecord>();
            public List<BoloRecord> Bolos = new List<BoloRecord>();
            public List<ReportRecord> Reports = new List<ReportRecord>();
            public List<FollowUp> FollowUps = new List<FollowUp>();
            public List<ShiftSummary> Shifts = new List<ShiftSummary>();
            public List<CaseAction> Actions = new List<CaseAction>();
            public ShiftSummary CurrentShift;
        }

        public static string PathIn(string folder)
        {
            return Path.Combine(folder ?? "", "TextDispatch.records.json");
        }

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
        }

        /// <summary>Write everything. Returns null on success, or what went wrong.</summary>
        public static string Save(string path, RecordsLedger ledger, ShiftLog shift)
        {
            try
            {
                if (shift != null) shift.Stamp();

                var file = new SaveFile
                {
                    Version = FormatVersion,
                    Saved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Unit = ledger.Unit,
                    CitationCount = ledger.CitationCount,
                    ArrestCount = ledger.ArrestCount,
                    FinesIssued = ledger.FinesIssued,
                    NextReport = ledger.NextReport,
                    Bolos = new List<BoloRecord>(ledger.Bolos),
                    Reports = new List<ReportRecord>(ledger.Reports),
                    FollowUps = new List<FollowUp>(ledger.FollowUps),
                    Shifts = new List<ShiftSummary>(ledger.Shifts),
                    Actions = new List<CaseAction>(ledger.Actions),
                    CurrentShift = shift == null ? null : shift.Current
                };

                // Everyone the player has touched, and anyone whose record has changed from what the
                // seed gave them. The seeded town comes back by itself; there is no need to store it.
                foreach (var person in ledger.People)
                    if (person.Met || person.Arrests > 0 || person.Citations > 0 || person.Notes.Count > 0)
                        file.People.Add(person);

                foreach (var vehicle in ledger.Vehicles) file.Vehicles.Add(vehicle);

                var json = Serializer().Serialize(file);

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

                var temporary = path + ".tmp";
                File.WriteAllText(temporary, json);

                if (File.Exists(path))
                {
                    try { File.Copy(path, path + ".bak", true); } catch { }
                    File.Delete(path);
                }
                File.Move(temporary, path);

                ledger.Dirty = false;
                return null;
            }
            catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
        }

        /// <summary>
        /// Put what is on disk back into a freshly seeded ledger. Returns the shift that was running when
        /// the file was written, if there was one, and says in `problem` why nothing could be read.
        /// </summary>
        public static ShiftSummary Load(string path, RecordsLedger ledger, out string problem)
        {
            problem = null;

            var file = Read(path, out problem);
            if (file == null && File.Exists(path + ".bak"))
            {
                string backup;
                file = Read(path + ".bak", out backup);
                if (file != null) problem = (problem ?? "") + " - the backup was used instead";
            }
            if (file == null) return null;

            if (!string.IsNullOrEmpty(file.Unit)) ledger.Unit = file.Unit;
            ledger.CitationCount = file.CitationCount;
            ledger.ArrestCount = file.ArrestCount;
            ledger.FinesIssued = file.FinesIssued;
            if (file.NextReport > ledger.NextReport) ledger.NextReport = file.NextReport;

            if (file.People != null) foreach (var person in file.People) ledger.Restore(person);
            if (file.Vehicles != null) foreach (var vehicle in file.Vehicles) ledger.Restore(vehicle);

            ledger.Bolos.Clear();
            if (file.Bolos != null) ledger.Bolos.AddRange(file.Bolos);
            if (file.Reports != null) ledger.Reports.AddRange(file.Reports);
            if (file.FollowUps != null)
                foreach (var followUp in file.FollowUps)
                    if (followUp != null) { if (followUp.Evidence == null) followUp.Evidence = new List<string>(); ledger.FollowUps.Add(followUp); }
            if (file.Actions != null) ledger.Actions.AddRange(file.Actions);
            if (file.Shifts != null)
                foreach (var shift in file.Shifts)
                    if (shift != null) { if (shift.Calls == null) shift.Calls = new List<string>(); ledger.Shifts.Add(shift); }

            ledger.Dirty = false;

            if (file.CurrentShift != null && file.CurrentShift.Calls == null) file.CurrentShift.Calls = new List<string>();
            return file.CurrentShift;
        }

        private static SaveFile Read(string path, out string problem)
        {
            problem = null;
            try
            {
                if (!File.Exists(path)) return null;
                var file = Serializer().Deserialize<SaveFile>(File.ReadAllText(path));
                if (file == null) problem = "the file was empty";
                return file;
            }
            catch (Exception ex)
            {
                problem = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }
    }
}
