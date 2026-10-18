using System;
using System.Collections.Generic;
using TextDispatch.Chat;
using TextDispatch.Records;

namespace TextDispatch.Commands
{
    /// <summary>
    /// /mdt: the mobile data computer. Each tab is one of the box's own lookups, run with its lines caught
    /// and put on the computer's screen instead - so what the computer shows is exactly what the command
    /// would have said.
    /// </summary>
    internal sealed partial class CommandRouter
    {
        /// <summary>The computer on screen. Set by the plugin after it is built.</summary>
        internal Computer Computer;

        private void Mdt(string argument)
        {
            if (Computer == null) { RecordsSummary(); return; }

            var text = (argument ?? "").Trim();
            var split = text.IndexOf(' ');
            var verb = (split < 0 ? text : text.Substring(0, split)).ToLowerInvariant();
            var rest = split < 0 ? "" : text.Substring(split + 1).Trim();

            switch (verb)
            {
                case "":
                    if (Computer.Open) { Computer.Open = false; return; }
                    _chat.Me("You", "flip open the laptop on the dash.");
                    Screen("HOME", "Home", Home);
                    return;
                case "close": case "off": case "exit": Computer.Open = false; return;
                case "next": case "more": case "down": Computer.Page(1); return;
                case "back": case "prev": case "up": Computer.Page(-1); return;
                case "home": Screen("HOME", "Home", Home); return;
                case "person": case "name": case "ped":
                    Screen("PERSON", rest.Length > 0 ? "Person search: " + rest : "Person: whoever is in front of you", () => PersonLookup(rest)); return;
                case "vehicle": case "plate": case "veh":
                    Screen("VEHICLE", rest.Length > 0 ? "Plate search: " + rest.ToUpperInvariant() : "Vehicle: the one you stopped", () => PlateLookup(rest)); return;
                case "warrants": case "warrant": case "wanted":
                    Screen("WARRANTS", "Outstanding warrants", () => { if (rest.Length > 0) WarrantLookup(rest); else Warrants(""); }); return;
                case "bolo": case "bolos":
                    Screen("BOLO", "Be on the lookout", () => Bolo("list")); return;
                case "cad": case "calls": case "incident": case "incidents":
                    Screen("CAD", rest.Length > 0 ? "Incident " + rest : "Computer-aided dispatch", () => Cad(rest)); return;
                case "units": case "board":
                    Screen("UNITS", "Units on the street", Board); return;
                case "reports": case "report":
                    Screen("REPORTS", "Reports" + (rest.Length > 0 ? " - " + rest : ""), () => ListReports(rest)); return;
                case "cases": case "case":
                    Screen("CASES", "Investigations", () => CaseCommand(rest.Length > 0 ? rest : "list")); return;
                case "flags": case "notes":
                    Screen("FLAGS", "Flags and notes" + (rest.Length > 0 ? " - " + rest : ""), () => ListFlags(rest)); return;
                case "msg": case "messages": case "inbox": case "phone":
                    Screen("MSG", "Messages" + (rest.Length > 0 ? " - " + rest : ""), () => Inbox(rest)); return;
            }

            // Anything else is a search: "/mdt Reyes", "/mdt 12ABC345".
            MdtSearch(text);
        }

        /// <summary>A name or a plate typed at the computer: a plate when it looks like one, a person otherwise.</summary>
        private void MdtSearch(string text)
        {
            var t = (text ?? "").Trim();
            if (t.Length == 0) return;
            if (t.StartsWith("/")) { Handle(t); return; }

            var compact = t.Replace(" ", "");
            var looksLikePlate = compact.Length >= 4 && compact.Length <= 8 && t.IndexOf(' ') < 0 && HasDigit(compact);
            if (looksLikePlate || _records.FindVehicle(compact) != null)
                Screen("VEHICLE", "Plate search: " + compact.ToUpperInvariant(), () => PlateLookup(compact));
            else
                Screen("PERSON", "Person search: " + t, () => PersonLookup(t));
        }

        private static bool HasDigit(string text)
        {
            foreach (var c in text) if (char.IsDigit(c)) return true;
            return false;
        }

        /// <summary>Run a lookup with its lines caught, and put them on the computer.</summary>
        private void Screen(string tab, string title, Action fill)
        {
            List<ChatLine> lines;
            _chat.BeginCapture();
            try { fill(); }
            catch (Exception ex) { Log.Error("computer " + tab, ex); }
            finally { lines = _chat.EndCapture(); }

            if (lines.Count == 0) lines.Add(new ChatLine { Channel = ChatChannel.Notice, Text = "No results." });
            Computer.Unit = _dispatch.Unit;
            Computer.Show(tab, title, lines);
        }

        /// <summary>The home screen: who you are, what is open, and what is waiting on you.</summary>
        private void Home()
        {
            var rank = Career.RankOf(_records, _shift.Current);
            _chat.Notice(rank.Title + " - " + _dispatch.Unit + "   " + (string.IsNullOrEmpty(_dispatch.LastStatus) ? "10-8" : _dispatch.LastStatus.ToUpperInvariant()) +
                         (_shift.Running ? "   on shift " + ShiftLog.Duration((int)_shift.Elapsed.TotalMinutes) : ""));

            var incident = _records.CurrentIncident;
            _chat.Notice(incident != null ? "ACTIVE CALL  " + incident.Number + "  " + incident.Priority + "  " + incident.Call : "No active call.");
            _chat.Notice("");

            RecordsSummary();
            _chat.Notice("");

            var waiting = new List<string>();
            var unbooked = _records.UnbookedArrests();
            if (unbooked.Count > 0) waiting.Add(unbooked.Count + " arrest(s) to book");
            var returned = _records.ReturnedReports();
            if (returned.Count > 0) waiting.Add(returned.Count + " report(s) sent back");
            var noReport = 0;
            foreach (var followUp in _records.FollowUps) if (followUp.Kind == "arrest" && !followUp.ReportFiled) noReport++;
            if (noReport > 0) waiting.Add(noReport + " arrest(s) with no report");
            var wanted = _records.KnownWanted();
            if (wanted.Count > 0) waiting.Add(wanted.Count + " known warrant(s)");
            _chat.Notice(waiting.Count == 0 ? "Nothing waiting on you." : "WAITING: " + string.Join(", ", waiting.ToArray()));

            if (_records.Bolos.Count > 0) _chat.Notice("BOLO: " + _records.Bolos[_records.Bolos.Count - 1].Subject + " - " + _records.Bolos[_records.Bolos.Count - 1].Reason);
            var last = _records.Messages.Count > 0 ? _records.Messages[_records.Messages.Count - 1] : null;
            if (last != null && !last.Outgoing) _chat.Phone("", "Last message - " + last.Contact + ": " + last.Text);
        }
    }
}
