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

            // 1.0.44: the world that remembers, and the career.
            public List<FlagRecord> Flags = new List<FlagRecord>();
            public List<CaseFile> Cases = new List<CaseFile>();
            public List<PhoneMessage> Messages = new List<PhoneMessage>();
            public List<string> Contacts = new List<string>();
            public int NextCase;
            public int ActiveCase;
            public string PlayerDescription;
            public int ChargedCount;
            public int ReportsApproved;
            public int ReportsReturned;
            public int CasesClosed;
            public string LastRank;

            // 1.0.47: call numbers and bookings.
            public List<IncidentRecord> Incidents = new List<IncidentRecord>();
            public List<BookingRecord> Bookings = new List<BookingRecord>();
            public int NextIncident;
            public int NextBooking;

            // 1.0.51
            public bool Tutored;

            // 1.0.54
            public string OfficerName;
            public string Badge;
            public List<string> ArmoryLog = new List<string>();
            public List<string> SignedOut = new List<string>();
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
                    CurrentShift = shift == null ? null : shift.Current,
                    Flags = new List<FlagRecord>(ledger.Flags),
                    Cases = new List<CaseFile>(ledger.Cases),
                    Messages = new List<PhoneMessage>(ledger.Messages),
                    Contacts = new List<string>(ledger.Contacts),
                    NextCase = ledger.NextCase,
                    ActiveCase = ledger.ActiveCase,
                    PlayerDescription = ledger.PlayerDescription,
                    ChargedCount = ledger.ChargedCount,
                    ReportsApproved = ledger.ReportsApproved,
                    ReportsReturned = ledger.ReportsReturned,
                    CasesClosed = ledger.CasesClosed,
                    LastRank = ledger.LastRank,
                    Incidents = new List<IncidentRecord>(ledger.Incidents),
                    Bookings = new List<BookingRecord>(ledger.Bookings),
                    NextIncident = ledger.NextIncident,
                    NextBooking = ledger.NextBooking,
                    Tutored = ledger.Tutored,
                    OfficerName = ledger.OfficerName,
                    Badge = ledger.Badge,
                    ArmoryLog = new List<string>(ledger.ArmoryLog),
                    SignedOut = new List<string>(ledger.SignedOut)
                };

                // Everyone the player has touched, and anyone whose record has changed from what the
                // seed gave them. The seeded town comes back by itself; there is no need to store it.
                foreach (var person in ledger.People)
                    if (person.Met || person.Arrests > 0 || person.Citations > 0 || person.Notes.Count > 0 || person.PendingCharges.Count > 0)
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
            if (file.Reports != null)
                foreach (var report in file.Reports)
                {
                    if (report == null) continue;
                    if (report.Charges == null) report.Charges = new List<string>();
                    ledger.Reports.Add(report);
                }
            if (file.FollowUps != null)
                foreach (var followUp in file.FollowUps)
                    if (followUp != null)
                    {
                        if (followUp.Evidence == null) followUp.Evidence = new List<string>();
                        if (followUp.Charges == null) followUp.Charges = new List<string>();
                        ledger.FollowUps.Add(followUp);
                    }
            if (file.Actions != null) ledger.Actions.AddRange(file.Actions);
            if (file.Shifts != null)
                foreach (var shift in file.Shifts)
                    if (shift != null) { if (shift.Calls == null) shift.Calls = new List<string>(); ledger.Shifts.Add(shift); }

            if (file.Flags != null) foreach (var flag in file.Flags) if (flag != null) ledger.Flags.Add(flag);
            if (file.Cases != null)
                foreach (var investigation in file.Cases)
                {
                    if (investigation == null) continue;
                    if (investigation.People == null) investigation.People = new List<string>();
                    if (investigation.Reports == null) investigation.Reports = new List<int>();
                    if (investigation.Notes == null) investigation.Notes = new List<string>();
                    ledger.Cases.Add(investigation);
                }
            if (file.Messages != null) foreach (var message in file.Messages) if (message != null) ledger.Messages.Add(message);
            if (file.Contacts != null) foreach (var contact in file.Contacts) if (!string.IsNullOrEmpty(contact) && !ledger.Contacts.Contains(contact)) ledger.Contacts.Add(contact);
            if (file.NextCase > ledger.NextCase) ledger.NextCase = file.NextCase;
            ledger.ActiveCase = file.ActiveCase;
            ledger.PlayerDescription = file.PlayerDescription;
            ledger.ChargedCount = file.ChargedCount;
            ledger.ReportsApproved = file.ReportsApproved;
            ledger.ReportsReturned = file.ReportsReturned;
            ledger.CasesClosed = file.CasesClosed;
            ledger.LastRank = file.LastRank;
            if (file.Incidents != null)
                foreach (var incident in file.Incidents)
                    if (incident != null) { if (incident.Log == null) incident.Log = new List<string>(); ledger.Incidents.Add(incident); }
            if (file.Bookings != null)
                foreach (var booking in file.Bookings)
                    if (booking != null)
                    {
                        if (booking.Charges == null) booking.Charges = new List<string>();
                        if (booking.Property == null) booking.Property = new List<string>();
                        ledger.Bookings.Add(booking);
                    }
            if (file.NextIncident > ledger.NextIncident) ledger.NextIncident = file.NextIncident;
            if (file.NextBooking > ledger.NextBooking) ledger.NextBooking = file.NextBooking;
            ledger.Tutored = file.Tutored;
            ledger.OfficerName = file.OfficerName;
            ledger.Badge = file.Badge;
            if (file.ArmoryLog != null) ledger.ArmoryLog.AddRange(file.ArmoryLog);
            if (file.SignedOut != null) ledger.SignedOut.AddRange(file.SignedOut);
            // An incident left open when the game closed is closed now - the call went with the session.
            var stale = ledger.CurrentIncident;
            if (stale != null) ledger.CloseIncident("the session ended");

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
