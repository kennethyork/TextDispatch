using System;
using System.Collections.Generic;
using System.Text;

namespace TextDispatch.Records
{
    /// <summary>
    /// A report written from what actually happened: the call, who it was about, and what the officer
    /// did to them, in order - the searches, the evidence, the rights, what they said, the arrest. It is
    /// a draft: it goes into the box for the player to finish in their own words, never straight onto
    /// file, because a report is the officer's account and not the plugin's.
    /// </summary>
    public static class ReportDraft
    {
        /// <summary>How far back a draft looks for things done to the person, when there is no call to go on.</summary>
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(45);

        public static string Build(RecordsLedger ledger, PersonRecord subject, string call, DateTime now)
        {
            var text = new StringBuilder();

            if (!string.IsNullOrEmpty(call)) text.Append("Responded to ").Append(call).Append(". ");

            var actions = new List<CaseAction>();
            foreach (var action in ledger.Actions)
            {
                if (now.Ticks - action.Ticks > Window.Ticks) continue;

                var forThem = subject != null && action.PersonId == subject.Id;
                var onThisCall = !string.IsNullOrEmpty(call) && string.Equals(action.Call, call, StringComparison.OrdinalIgnoreCase);

                // About this person, on this call (or recently, when there is no call); or about nobody, on this call.
                if (subject != null ? forThem && (onThisCall || string.IsNullOrEmpty(call) || action.Call == null) : onThisCall)
                    actions.Add(action);
            }

            if (subject != null)
            {
                text.Append("Made contact with ").Append(subject.Name);
                if (subject.Age > 0) text.Append(", ").Append(subject.Age);
                text.Append(". ");
                if (subject.Wanted && !string.IsNullOrEmpty(subject.WarrantFor))
                    text.Append("Records showed an outstanding warrant for ").Append(subject.WarrantFor).Append(". ");
            }

            foreach (var action in actions)
                text.Append(Sentence(action.Text)).Append(' ');

            if (subject != null)
            {
                var evidence = ledger.EvidenceOn(subject);
                if (evidence.Count > 0)
                    text.Append("Evidence: ").Append(string.Join("; ", evidence.ToArray())).Append(". ");
            }

            if (subject == null && actions.Count == 0 && string.IsNullOrEmpty(call))
                return "";

            return text.ToString().Trim();
        }

        /// <summary>"searched the vehicle" -> "Searched the vehicle."</summary>
        private static string Sentence(string text)
        {
            var t = (text ?? "").Trim();
            if (t.Length == 0) return t;
            t = char.ToUpperInvariant(t[0]) + t.Substring(1);
            if (!t.EndsWith(".")) t += ".";
            return t;
        }
    }
}
