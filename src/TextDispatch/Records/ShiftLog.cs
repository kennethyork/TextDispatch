using System;
using System.Collections.Generic;

namespace TextDispatch.Records
{
    /// <summary>
    /// The shift in progress: when it started and what has happened on it.
    ///
    /// A shift starts when the player goes on duty or comes back 10-8, and ends with 10-7 or going off
    /// duty - at which point dispatch reads the summary back and it goes into the history on file.
    /// It has no reference to the game, like the ledger, so the counting can be checked outside it.
    /// </summary>
    public sealed class ShiftLog
    {
        private ShiftSummary _current;
        private DateTime _startedAt;

        public bool Running { get { return _current != null; } }
        public ShiftSummary Current { get { return _current; } }

        public TimeSpan Elapsed
        {
            get { return _current == null ? TimeSpan.Zero : DateTime.Now - _startedAt; }
        }

        public void Start(string unit)
        {
            if (_current != null) return;

            _startedAt = DateTime.Now;
            _current = new ShiftSummary
            {
                Unit = unit,
                Started = _startedAt.ToString("yyyy-MM-dd HH:mm")
            };
        }

        /// <summary>
        /// Carry on a shift that was in progress when the game closed, if it is recent enough to be the
        /// same one; otherwise it is filed as unfinished and a fresh one is started.
        /// </summary>
        public ShiftSummary Resume(ShiftSummary saved, string unit)
        {
            if (saved == null) { Start(unit); return null; }

            // The same shift if the game was last running it under twenty minutes ago - a crash or a
            // quick restart - and a new one otherwise.
            DateTime started, lastAlive;
            if (DateTime.TryParse(saved.Started, out started) &&
                DateTime.TryParse(saved.Ended ?? saved.Started, out lastAlive) &&
                DateTime.Now - lastAlive < TimeSpan.FromMinutes(20))
            {
                _current = saved;
                _current.Ended = null;
                _startedAt = started;
                return null;
            }

            saved.Unfinished = true;
            if (saved.Ended == null) saved.Ended = saved.Started;
            Start(unit);
            return saved;
        }

        /// <summary>End the shift and return what it came to, or null if there was none worth keeping.</summary>
        public ShiftSummary End()
        {
            if (_current == null) return null;

            var shift = _current;
            _current = null;

            shift.Ended = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            shift.Minutes = (int)Math.Round((DateTime.Now - _startedAt).TotalMinutes);
            return shift;
        }

        /// <summary>Stamp the time on the shift in progress, so a saved one says when it was last alive.</summary>
        public void Stamp()
        {
            if (_current == null) return;
            _current.Ended = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            _current.Minutes = (int)Math.Round((DateTime.Now - _startedAt).TotalMinutes);
        }

        // ------------------------------------------------------------------ counting

        public void Call(string name)
        {
            if (_current == null || string.IsNullOrEmpty(name)) return;
            _current.Calls.Add(name);
        }

        public void Stop() { if (_current != null) _current.Stops++; }
        public void Pursuit() { if (_current != null) _current.Pursuits++; }
        public void Arrest() { if (_current != null) _current.Arrests++; }
        public void Backup() { if (_current != null) _current.Backups++; }
        public void Report() { if (_current != null) _current.Reports++; }

        public void Citation(double fine)
        {
            if (_current == null) return;
            _current.Citations++;
            _current.Fines += fine;
        }

        public void Search(bool found)
        {
            if (_current == null) return;
            _current.Searches++;
            if (found) _current.Finds++;
        }

        // ------------------------------------------------------------------ saying it

        /// <summary>The summary as dispatch reads it back: a headline and a few lines.</summary>
        public static List<string> Describe(ShiftSummary shift)
        {
            var lines = new List<string>();
            if (shift == null) return lines;

            lines.Add(Duration(shift.Minutes) + " on shift" +
                      (string.IsNullOrEmpty(shift.Started) ? "" : ", from " + Clock(shift.Started)) +
                      (string.IsNullOrEmpty(shift.Ended) ? "" : " to " + Clock(shift.Ended)) +
                      (shift.Unfinished ? " (the game closed before it was ended)" : "") + ".");

            lines.Add(Count(shift.Calls.Count, "call") + ", " + Count(shift.Stops, "traffic stop") + ", " +
                      Count(shift.Pursuits, "pursuit") + ".");

            lines.Add(Count(shift.Arrests, "arrest") + ", " + Count(shift.Citations, "citation") +
                      (shift.Fines > 0 ? " ($" + shift.Fines.ToString("0.00") + " in fines)" : "") + ", " +
                      Count(shift.Searches, "vehicle search", "vehicle searches") +
                      (shift.Searches > 0 ? " (" + shift.Finds + " found something)" : "") + ", " +
                      Count(shift.Reports, "report") + ".");

            if (shift.Calls.Count > 0)
            {
                var names = new List<string>();
                for (var i = 0; i < shift.Calls.Count && i < 6; i++) names.Add(shift.Calls[i]);
                lines.Add("Calls: " + string.Join(", ", names.ToArray()) +
                          (shift.Calls.Count > 6 ? " and " + (shift.Calls.Count - 6) + " more" : "") + ".");
            }

            return lines;
        }

        public static string Duration(int minutes)
        {
            if (minutes < 60) return minutes + " min";
            return (minutes / 60) + "h " + (minutes % 60).ToString("00") + "m";
        }

        private static string Clock(string when)
        {
            DateTime parsed;
            return DateTime.TryParse(when, out parsed) ? parsed.ToString("HH:mm") : when;
        }

        private static string Count(int n, string one, string many = null)
        {
            return n + " " + (n == 1 ? one : many ?? one + "s");
        }
    }
}
