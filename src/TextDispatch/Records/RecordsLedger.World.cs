using System;
using System.Collections.Generic;

namespace TextDispatch.Records
{
    /// <summary>A flag or a note on a person or a plate - what the next officer to run them should know.</summary>
    public sealed class FlagRecord
    {
        public string Subject;   // a person's name or a plate, as it was typed
        public string Kind;      // "flag" (a warning, shown loudly) or "note"
        public string Text;
        public string When;      // local time, "yyyy-MM-dd HH:mm"
    }

    /// <summary>
    /// An investigation: several calls, reports, people and pieces of evidence that are one matter. GTA
    /// World's detectives keep these as forum threads; here it is a case number the reports go under.
    /// </summary>
    public sealed class CaseFile
    {
        public int Number;
        public string Title;
        public string Opened;    // local time
        public string Closed;    // local time, or null while open
        public string Outcome;
        public List<string> People = new List<string>();
        public List<int> Reports = new List<int>();
        public List<string> Notes = new List<string>();
    }

    /// <summary>One text message, to or from a contact.</summary>
    public sealed class PhoneMessage
    {
        public string Contact;
        public bool Outgoing;
        public string Text;
        public string When;      // local time
    }

    /// <summary>
    /// The parts of the records that make the world remember: flags and notes, cases, the phone, the
    /// officer's own description and career - and the people who come back.
    /// </summary>
    public sealed partial class RecordsLedger
    {
        private readonly List<FlagRecord> _flags = new List<FlagRecord>();
        private readonly List<CaseFile> _cases = new List<CaseFile>();
        private readonly List<PhoneMessage> _messages = new List<PhoneMessage>();
        private readonly List<string> _contacts = new List<string>();

        /// <summary>People saved in the phone, besides the department's own numbers.</summary>
        public List<string> Contacts { get { return _contacts; } }

        public List<FlagRecord> Flags { get { return _flags; } }
        public List<CaseFile> Cases { get { return _cases; } }
        public List<PhoneMessage> Messages { get { return _messages; } }

        public int NextCase { get; internal set; } = 1;

        /// <summary>The case new reports are filed under, or 0. Set by /case use, cleared by /case close.</summary>
        public int ActiveCase { get; internal set; }

        /// <summary>How the officer looks, written by the player with /my - shown on /examine me, and seen by the people they talk to.</summary>
        public string PlayerDescription { get; internal set; }

        // The career, counted where it happens, so the rank does not have to be worked out from history.
        public int ChargedCount { get; internal set; }
        public int ReportsApproved { get; internal set; }
        public int ReportsReturned { get; internal set; }
        public int CasesClosed { get; internal set; }

        /// <summary>The rank last said out loud, so a promotion is announced once.</summary>
        public string LastRank { get; internal set; }

        public void SetPlayerDescription(string text)
        {
            PlayerDescription = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            Dirty = true;
        }

        // ------------------------------------------------------------------ flags and notes

        public FlagRecord AddFlag(string subject, string kind, string text)
        {
            var flag = new FlagRecord
            {
                Subject = (subject ?? "").Trim(),
                Kind = kind,
                Text = (text ?? "").Trim(),
                When = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            _flags.Add(flag);
            if (_flags.Count > 500) _flags.RemoveAt(0);
            Dirty = true;
            return flag;
        }

        /// <summary>Flags and notes on a person (by name) or a plate, oldest first.</summary>
        public List<FlagRecord> FlagsOn(string subject)
        {
            var found = new List<FlagRecord>();
            if (string.IsNullOrWhiteSpace(subject)) return found;
            var plate = PlateKey(subject);
            foreach (var flag in _flags)
                if (string.Equals(flag.Subject, subject, StringComparison.OrdinalIgnoreCase) ||
                    (plate.Length > 0 && PlateKey(flag.Subject) == plate))
                    found.Add(flag);
            return found;
        }

        public int ClearFlags(string subject)
        {
            var on = FlagsOn(subject);
            foreach (var flag in on) _flags.Remove(flag);
            if (on.Count > 0) Dirty = true;
            return on.Count;
        }

        /// <summary>A BOLO that matches this person or plate, or null - so a stop on it says so.</summary>
        public BoloRecord BoloFor(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject)) return null;
            var plate = PlateKey(subject);
            foreach (var bolo in Bolos)
            {
                if (string.Equals(bolo.Subject, subject, StringComparison.OrdinalIgnoreCase)) return bolo;
                if (plate.Length > 0 && PlateKey(bolo.Subject) == plate) return bolo;
                // A BOLO on a surname finds the whole name.
                if (bolo.Subject.Length >= 4 && subject.IndexOf(bolo.Subject, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    subject.IndexOf(' ') > 0) return bolo;
            }
            return null;
        }

        // ------------------------------------------------------------------ cases

        public CaseFile OpenInvestigation(string title)
        {
            var file = new CaseFile
            {
                Number = NextCase++,
                Title = string.IsNullOrWhiteSpace(title) ? "Untitled investigation" : title.Trim(),
                Opened = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            _cases.Add(file);
            ActiveCase = file.Number;
            Dirty = true;
            return file;
        }

        public CaseFile FindCase(int number)
        {
            foreach (var file in _cases) if (file.Number == number) return file;
            return null;
        }

        public void UseCase(int number)
        {
            ActiveCase = number;
            Dirty = true;
        }

        public void CloseCase(CaseFile file, string outcome)
        {
            if (file == null || file.Closed != null) return;
            file.Closed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            file.Outcome = string.IsNullOrWhiteSpace(outcome) ? "closed" : outcome.Trim();
            if (ActiveCase == file.Number) ActiveCase = 0;
            CasesClosed++;
            Dirty = true;
        }

        public void AddToCase(CaseFile file, PersonRecord person)
        {
            if (file == null || person == null) return;
            if (!file.People.Contains(person.Name)) file.People.Add(person.Name);
            Touch(person);
        }

        public void NoteOnCase(CaseFile file, string text)
        {
            if (file == null || string.IsNullOrWhiteSpace(text)) return;
            file.Notes.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ": " + text.Trim());
            Dirty = true;
        }

        /// <summary>A report filed while a case is in use goes under it, and so does the person it is about.</summary>
        private void AttachReport(ReportRecord report)
        {
            var file = FindCase(report.Case);
            if (file == null) { report.Case = 0; return; }
            if (!file.Reports.Contains(report.Number)) file.Reports.Add(report.Number);
            if (!string.IsNullOrEmpty(report.Subject) && !file.People.Contains(report.Subject)) file.People.Add(report.Subject);
        }

        public ReportRecord FindReport(int number)
        {
            foreach (var report in Reports) if (report.Number == number) return report;
            return null;
        }

        // ------------------------------------------------------------------ the sergeant's review

        /// <summary>The first report the sergeant has got round to, or null.</summary>
        public ReportRecord DueReview(DateTime utcNow)
        {
            foreach (var report in Reports)
                if (report.Review == null && report.ReviewDueTicks > 0 && report.ReviewDueTicks <= utcNow.Ticks) return report;
            return null;
        }

        /// <summary>
        /// Read a report the way a watch sergeant does on GTA World's forums: first person, long enough to
        /// say what was seen and done and why, the charge said, the evidence listed, the rights mentioned
        /// when there is a statement. One thing to fix is a note on an approval; more is a report sent back,
        /// and a report sent back does not go to the DA until it is fixed.
        /// </summary>
        public bool ReviewReport(ReportRecord report, out List<string> notes)
        {
            notes = new List<string>();
            var text = " " + (report.Text ?? "").ToLowerInvariant() + " ";
            var words = (report.Text ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

            if (words < 20) notes.Add("It is too short - say what you saw, what you did and why.");
            if (text.IndexOf(" i ", StringComparison.Ordinal) < 0 && text.IndexOf(" i'", StringComparison.Ordinal) < 0 &&
                text.IndexOf(" my ", StringComparison.Ordinal) < 0)
                notes.Add("Write it in the first person - \"I stopped the vehicle\", not \"the vehicle was stopped\".");

            var person = report.Subject == null ? null : FindPerson(report.Subject);
            if (person != null)
            {
                var open = OpenCase(person);
                var charged = (open != null && open.Charges.Count > 0) || report.Charges.Count > 0;
                if ((open != null || charged) && !Mentions(text, "arrest", "charge", "booked", "pc ", "custody"))
                    notes.Add("Say what " + person.Name + " was arrested for, and on what grounds.");

                if (EvidenceOn(person).Count > 0 && !Mentions(text, "evidence", "found", "seized", "recovered", "located", "bagged"))
                    notes.Add("List the evidence and where it was found.");

                var statement = open != null ? open.Statement : person.Statement;
                if (statement != null && !Mentions(text, "miranda", "rights"))
                    notes.Add("Say whether they were read their rights before they made a statement.");
            }

            var approved = words >= 20 && notes.Count <= 1;
            report.Review = approved ? "approved" : "returned";
            report.ReviewNotes = notes.Count == 0 ? null : string.Join(" ", notes.ToArray());
            report.ReviewDueTicks = 0;

            if (approved) ReportsApproved++;
            else
            {
                ReportsReturned++;
                // Sent back is not on file for the DA: the case waits for a report that will stand up.
                if (person != null && !HasStandingReport(person))
                    foreach (var followUp in FollowUps)
                        if (followUp.PersonId == person.Id) followUp.ReportFiled = false;
            }
            Dirty = true;
            return approved;
        }

        /// <summary>Put more in a report and send it back to the sergeant.</summary>
        public void AmendReport(ReportRecord report, string more)
        {
            if (report == null || string.IsNullOrWhiteSpace(more)) return;
            report.Text = (report.Text ?? "").TrimEnd() + " " + more.Trim();
            report.Review = null;
            report.ReviewNotes = null;
            report.ReviewDueTicks = DateTime.UtcNow.AddSeconds(45 + new Random(Environment.TickCount).Next(60)).Ticks;

            var person = report.Subject == null ? null : FindPerson(report.Subject);
            if (person != null)
                foreach (var followUp in FollowUps)
                    if (followUp.PersonId == person.Id) followUp.ReportFiled = true;
            Dirty = true;
        }

        /// <summary>A report on them that is approved or still with the sergeant.</summary>
        private bool HasStandingReport(PersonRecord person)
        {
            foreach (var report in Reports)
                if (string.Equals(report.Subject, person.Name, StringComparison.OrdinalIgnoreCase) && report.Review != "returned")
                    return true;
            return false;
        }

        public List<ReportRecord> ReturnedReports()
        {
            var found = new List<ReportRecord>();
            foreach (var report in Reports) if (report.Review == "returned") found.Add(report);
            return found;
        }

        private static bool Mentions(string text, params string[] words)
        {
            foreach (var word in words) if (text.IndexOf(word, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        // ------------------------------------------------------------------ the people who come back

        private readonly HashSet<string> _returnedThisSession = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _recognised = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _lastReturnAt = Environment.TickCount - 600000;

        /// <summary>
        /// Somebody the officer has dealt with before, turning up again as this person - or null.
        ///
        /// Decided once per person in the world, from the handle, so the same ped is the same answer. Rare
        /// on purpose: one in twenty-five new faces, no more than once in eight minutes, and never the same
        /// returning person twice in a session - a town where every second face is a regular is not a town.
        /// Only people with a history with this officer come back: an arrest, a ticket or a report.
        /// </summary>
        public PersonRecord ReturningFor(int handle)
        {
            if (Environment.TickCount - _lastReturnAt < 8 * 60000) return null;
            if (new Random(unchecked(handle * 977 + 41)).Next(25) != 0) return null;

            var pool = new List<PersonRecord>();
            foreach (var person in _people.Values)
                if (person.Met && (person.Arrests > 0 || person.Citations > 0 || ReportsAbout(person).Count > 0) &&
                    !_returnedThisSession.Contains(person.Id) && !_byHandle.ContainsValue(person.Id))
                    pool.Add(person);
            if (pool.Count == 0) return null;

            var back = pool[new Random(unchecked(handle * 131 + 5)).Next(pool.Count)];
            _returnedThisSession.Add(back.Id);
            _lastReturnAt = Environment.TickCount;
            _byHandle[handle] = back.Id;
            Log.Line("records: " + back.Name + " is back, as ped " + handle);
            return back;
        }

        /// <summary>
        /// What the officer remembers about a returning person, the first time they are face to face this
        /// session - or null if they are not one, or it has been said.
        /// </summary>
        public string Recognise(PersonRecord person)
        {
            if (person == null || !_returnedThisSession.Contains(person.Id) || !_recognised.Add(person.Id)) return null;

            var last = person.Notes.Count > 0 ? person.Notes[person.Notes.Count - 1] : null;
            var history = person.Arrests > 0
                ? "you have arrested them " + (person.Arrests == 1 ? "before" : person.Arrests + " times") +
                  (person.Priors.Count > 0 ? ", last for " + person.Priors[person.Priors.Count - 1] : "")
                : person.Citations > 0 ? "you have ticketed them before" : "you wrote a report on them";
            return "You recognise " + person.Name + " - " + history + "." + (last != null ? " Last on file: " + last + "." : "");
        }
    }
}
