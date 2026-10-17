using System;
using System.Collections.Generic;

namespace TextDispatch.Records
{
    /// <summary>
    /// Rank, certifications and the start-of-watch briefing.
    ///
    /// GTA World's departments promote on time and on the quality of the paperwork, and certify officers
    /// for the specialist work - K9, air, traffic, detective. In single player the record of what you have
    /// done is the only judge there is, so the rank is worked out from it: shifts worked, arrests that the
    /// DA charged, reports the sergeant approved, cases closed. A report sent back counts against you.
    ///
    /// No reference to the game, so the arithmetic can be checked outside it.
    /// </summary>
    public static class Career
    {
        public sealed class Rank
        {
            public string Title;
            public int Points;
            public string Next;     // null at the top
            public int ToNext;
        }

        private static readonly KeyValuePair<string, int>[] Ladder =
        {
            new KeyValuePair<string, int>("Police Officer I", 0),
            new KeyValuePair<string, int>("Police Officer II", 60),
            new KeyValuePair<string, int>("Police Officer III", 160),
            new KeyValuePair<string, int>("Senior Lead Officer", 320),
            new KeyValuePair<string, int>("Sergeant I", 550),
            new KeyValuePair<string, int>("Sergeant II", 850),
            new KeyValuePair<string, int>("Lieutenant", 1250),
            new KeyValuePair<string, int>("Captain", 1800)
        };

        public sealed class Totals
        {
            public int Shifts, Calls, Stops, Pursuits, Arrests, Citations, Searches, Finds, Reports;
        }

        /// <summary>Everything on file, the shift in progress included.</summary>
        public static Totals Count(RecordsLedger ledger, ShiftSummary current = null)
        {
            var totals = new Totals { Arrests = ledger.ArrestCount, Citations = ledger.CitationCount, Reports = ledger.Reports.Count };
            var shifts = new List<ShiftSummary>(ledger.Shifts);
            if (current != null) shifts.Add(current);
            foreach (var shift in shifts)
            {
                if (shift == null) continue;
                if (shift.Minutes >= 20) totals.Shifts++;
                totals.Calls += shift.Calls == null ? 0 : shift.Calls.Count;
                totals.Stops += shift.Stops;
                totals.Pursuits += shift.Pursuits;
                totals.Searches += shift.Searches;
                totals.Finds += shift.Finds;
            }
            return totals;
        }

        public static int Points(RecordsLedger ledger, ShiftSummary current = null)
        {
            var t = Count(ledger, current);
            var points = t.Shifts * 5 + t.Calls * 2 + t.Arrests * 3 + t.Citations + t.Finds * 2 + t.Pursuits * 2 +
                         ledger.ReportsApproved * 4 + ledger.ChargedCount * 4 + ledger.CasesClosed * 8 - ledger.ReportsReturned * 2;
            return Math.Max(0, points);
        }

        public static Rank RankOf(RecordsLedger ledger, ShiftSummary current = null)
        {
            var points = Points(ledger, current);
            var rank = new Rank { Title = Ladder[0].Key, Points = points };
            for (var i = 0; i < Ladder.Length; i++)
            {
                if (points < Ladder[i].Value) break;
                rank.Title = Ladder[i].Key;
                rank.Next = i + 1 < Ladder.Length ? Ladder[i + 1].Key : null;
                rank.ToNext = i + 1 < Ladder.Length ? Ladder[i + 1].Value - points : 0;
            }
            return rank;
        }

        public sealed class Certification
        {
            public string Key;       // "k9"
            public string Name;      // "K9 handler"
            public bool Held;
            public string Progress;  // what is still needed, or what earned it
        }

        /// <summary>The specialist certifications, and how far off each one is.</summary>
        public static List<Certification> Certifications(RecordsLedger ledger, ShiftSummary current = null)
        {
            var t = Count(ledger, current);
            var rank = RankOf(ledger, current);
            var list = new List<Certification>
            {
                Cert("traffic", "Traffic enforcement", Need(t.Stops, 10, "traffic stops"), Need(t.Citations, 25, "citations")),
                Cert("k9", "K9 handler", Need(t.Searches, 15, "vehicle searches"), Need(t.Finds, 6, "searches that found something")),
                Cert("air", "Air support observer", Need(t.Pursuits, 8, "pursuits"), Need(t.Shifts, 10, "shifts")),
                Cert("detective", "Detective", Need(ledger.CasesClosed, 3, "cases closed"), Need(ledger.ChargedCount, 8, "arrests the DA charged")),
                Cert("fto", "Field training officer", Need(ledger.ReportsApproved, 15, "approved reports"),
                     rank.Points >= 160 ? null : "rank of Police Officer III")
            };
            return list;
        }

        public static bool Holds(RecordsLedger ledger, string key, ShiftSummary current = null)
        {
            foreach (var cert in Certifications(ledger, current))
                if (cert.Key == key) return cert.Held;
            return true;
        }

        private static string Need(int have, int need, string what)
        {
            return have >= need ? null : (need - have) + " more " + what;
        }

        private static Certification Cert(string key, string name, params string[] missing)
        {
            var still = new List<string>();
            foreach (var m in missing) if (m != null) still.Add(m);
            return new Certification
            {
                Key = key,
                Name = name,
                Held = still.Count == 0,
                Progress = still.Count == 0 ? "certified" : string.Join(", ", still.ToArray())
            };
        }

        // ------------------------------------------------------------------ roll call

        private static readonly string[] Areas =
        {
            "Mission Row and Strawberry", "Vespucci and Del Perro", "Vinewood and Rockford Hills", "Davis and Rancho",
            "La Mesa and Cypress Flats", "Little Seoul and Pillbox Hill", "the Port and Elysian Island", "Mirror Park and East Vinewood",
            "Sandy Shores and Grand Senora", "Paleto Bay and the Great Ocean Highway"
        };

        private static readonly string[] Signoffs =
        {
            "Watch yourselves out there.", "Let's do our jobs and all go home.", "Paperwork before end of watch. That's all.",
            "Be safe, be smart.", "Hit the streets."
        };

        /// <summary>
        /// The briefing at the start of a watch: today's assignment, the BOLOs, who is wanted, the open
        /// cases, what the DA is waiting on, the reports sent back - read by the watch sergeant from what is
        /// actually on file, so it is worth listening to.
        /// </summary>
        public static List<string> RollCall(RecordsLedger ledger, string unit, ShiftSummary current)
        {
            var now = DateTime.Now;
            var lines = new List<string>();
            var greeting = now.Hour < 12 ? "Morning" : now.Hour < 18 ? "Afternoon" : "Evening";
            var rank = RankOf(ledger, current);

            lines.Add(greeting + ", " + rank.Title + " - " + unit + ". " + now.ToString("dddd d MMMM") + ".");
            lines.Add("Assignment: patrol " + Areas[(now.DayOfYear + now.Hour / 8) % Areas.Length] + ".");

            if (ledger.Bolos.Count == 0) lines.Add("No BOLOs out.");
            else
            {
                var bolos = new List<string>();
                for (var i = Math.Max(0, ledger.Bolos.Count - 3); i < ledger.Bolos.Count; i++)
                    bolos.Add(ledger.Bolos[i].Subject + " (" + ledger.Bolos[i].Reason + ")");
                lines.Add("BOLOs: " + string.Join("; ", bolos.ToArray()) + (ledger.Bolos.Count > 3 ? " - and " + (ledger.Bolos.Count - 3) + " more on /bolo." : "."));
            }

            var wanted = ledger.KnownWanted();
            if (wanted.Count > 0)
                lines.Add(wanted.Count + " of the people you've dealt with have warrants out - " + wanted[0].Name + " on " +
                          wanted[0].WarrantFor + (wanted.Count > 1 ? ", and others on /warrants." : "."));

            var open = 0;
            foreach (var file in ledger.Cases) if (file.Closed == null) open++;
            if (open > 0)
            {
                var active = ledger.FindCase(ledger.ActiveCase);
                lines.Add(open + " open investigation(s)" + (active != null ? ", working #" + active.Number + " " + active.Title : "") + ".");
            }

            if (ledger.FollowUps.Count > 0) lines.Add(ledger.FollowUps.Count + " case(s) with the DA.");

            var returned = ledger.ReturnedReports();
            if (returned.Count > 0)
                lines.Add("I sent back " + returned.Count + " of your reports - #" + returned[0].Number + " first. Fix them with /report fix.");

            lines.Add(Signoffs[now.DayOfYear % Signoffs.Length]);
            return lines;
        }
    }
}
