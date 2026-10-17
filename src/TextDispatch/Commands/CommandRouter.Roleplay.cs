using System;
using System.Collections.Generic;
using System.Globalization;
using Rage;
using TextDispatch.Chat;
using TextDispatch.Dialogue;
using TextDispatch.Lspdfr;
using TextDispatch.Records;

namespace TextDispatch.Commands
{
    /// <summary>
    /// The GTA World half of the command set: the roleplay commands, the paperwork as forum posts, the
    /// penal code, the records that remember, the career, the phone and the radar.
    /// </summary>
    internal sealed partial class CommandRouter
    {
        /// <summary>The traffic radar. Set by the plugin after it is built.</summary>
        internal Radar Radar;

        // ------------------------------------------------------------------ roleplay

        /// <summary>/ame: an action shown over your head for a few seconds, and in the box.</summary>
        private void Ame(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument)) { _chat.Error("Usage: /ame <action>   e.g. /ame glances at the plate"); return; }
            var text = argument.Trim();
            _chat.Write(ChatChannel.Me, "", "> You " + text);
            Overhead.Show(text, 4 + text.Length / 12);
            _dialogue.Note("You " + text);
        }

        /// <summary>
        /// /attempt: GTA World's coin toss for what the game cannot decide - forcing a door, catching a
        /// thrown bag. Even odds, said for everybody to see, and the result is binding.
        /// </summary>
        private void Attempt(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument)) { _chat.Error("Usage: /attempt <action>   e.g. /attempt to force the door open"); return; }
            var action = argument.Trim();
            if (action.StartsWith("to ", StringComparison.OrdinalIgnoreCase)) action = action.Substring(3);
            var success = new Random(unchecked(Environment.TickCount * 7919)).Next(2) == 0;
            _chat.Me("You", "attempt to " + action + " and " + (success ? "succeed." : "fail."));
            _dialogue.Note("You attempted to " + action + " and " + (success ? "succeeded" : "failed"));
        }

        /// <summary>/roll [sides]: a die, out loud. A d6 unless told otherwise.</summary>
        private void Roll(string argument)
        {
            int sides;
            if (!int.TryParse((argument ?? "").Trim().TrimStart('d', 'D'), out sides) || sides < 2 || sides > 1000) sides = 6;
            var result = 1 + new Random(unchecked(Environment.TickCount * 31)).Next(sides);
            _chat.Me("You", "roll a d" + sides + " and get " + result + ".");
        }

        /// <summary>/my: how your character looks. What /examine me shows, and what the people you talk to see.</summary>
        private void My(string argument)
        {
            var text = (argument ?? "").Trim();
            if (text.Length == 0)
            {
                _chat.Notice(string.IsNullOrEmpty(_records.PlayerDescription)
                    ? "No description yet. '/my <how you look>' - e.g. /my tall, shaved head, a scar over the left eyebrow, smells of coffee"
                    : "You: " + _records.PlayerDescription + "   ('/my clear' removes it)");
                return;
            }
            if (text.Equals("clear", StringComparison.OrdinalIgnoreCase)) { _records.SetPlayerDescription(null); _chat.Notice("Description cleared."); return; }

            _records.SetPlayerDescription(text);
            _chat.Notice("Description saved. People you talk to see it, and '/examine me' shows it.");
        }

        private static readonly string[] Builds = { "slim", "average", "heavy-set", "athletic", "wiry", "stocky" };
        private static readonly string[] Heights = { "short", "medium height", "tall", "very tall" };
        private static readonly string[] Hair = { "short dark hair", "long dark hair", "blond hair", "a shaved head", "greying hair", "red hair", "braids", "a buzz cut" };
        private static readonly string[] Clothes =
        {
            "a hoodie and jeans", "a work uniform with a name badge", "a tracksuit", "a cheap suit", "shorts and a tank top",
            "a leather jacket", "scrubs", "a hi-vis vest over work clothes", "a band t-shirt and ripped jeans", "a sundress"
        };
        private static readonly string[] Marks =
        {
            null, null, null, "a tattoo on the neck", "a sleeve of tattoos on the right arm", "a scar across the chin",
            "a fresh bruise under the eye", "a nose ring", "a limp on the left leg", "gold teeth"
        };

        /// <summary>
        /// /examine: what you see, written out - somebody's build, clothes, marks and manner, or a car's
        /// condition - so a stop can be played through text. The same person always looks the same.
        /// </summary>
        private void Examine(string argument)
        {
            var what = (argument ?? "").Trim().ToLowerInvariant();
            if (what == "me" || what == "self" || what == "myself")
            {
                _chat.Do(string.IsNullOrEmpty(_records.PlayerDescription) ? "You have not described yourself - /my <description>." : "You: " + _records.PlayerDescription);
                return;
            }

            if (IsVehicleWord(what)) { ExamineVehicle(); return; }

            var ped = Subject(8f);
            if (ped == null)
            {
                if (_api.NearestOtherVehicle(8f) != null) { ExamineVehicle(); return; }
                _chat.Error("Nobody and nothing close enough to look at.");
                return;
            }

            var handle = unchecked((int)ped.Handle.Value);
            var person = PersonInFront();
            var rng = new Random(unchecked(handle * 4111 + 3));
            bool male = true;
            try { male = ped.IsMale; } catch { }

            var parts = new List<string>
            {
                (male ? "A man" : "A woman") + (person != null && person.Age > 0 ? " of about " + (person.Age / 5 * 5) : "") + ", " +
                Heights[rng.Next(Heights.Length)] + " and " + Builds[rng.Next(Builds.Length)] + ", with " + Hair[rng.Next(Hair.Length)],
                "wearing " + Clothes[rng.Next(Clothes.Length)]
            };
            var mark = Marks[rng.Next(Marks.Length)];
            if (mark != null) parts.Add(mark);

            try
            {
                if (ped.Health < ped.MaxHealth * 0.7f) parts.Add("visibly hurt - bleeding, favouring one side");
                if (_api.PedCarryingContraband(ped)) parts.Add("keeps a hand near one pocket and won't hold eye contact");
            }
            catch { }

            // A smell of drink is decided once per person, and says what to do about it.
            if (rng.Next(100) < 10 || (person != null && person.Priors.Contains("DUI") && rng.Next(3) == 0))
                parts.Add("smells of alcohol, eyes glassy, speech a little slurred");

            switch (Identities.MoodFor(handle))
            {
                case Temperament.Nervous: parts.Add("fidgeting, glancing around"); break;
                case Temperament.Defensive: parts.Add("arms folded, jaw set"); break;
                case Temperament.Hostile: parts.Add("squared up, glaring at you"); break;
                default: parts.Add("calm, hands where you can see them"); break;
            }

            _chat.Do((person != null ? person.Name + ": " : "") + string.Join("; ", parts.ToArray()) + ".");
            _chat.Me("You", "look them over.");
        }

        private void ExamineVehicle()
        {
            var vehicle = _api.PulloverVehicle() ?? _api.NearestOtherVehicle(10f);
            if (vehicle == null) { _chat.Error("No vehicle close enough to look at."); return; }

            var parts = new List<string>();
            string plate = "no plate", model = "vehicle";
            try { plate = vehicle.LicensePlate.Trim(); } catch { }
            try { model = vehicle.Model.Name; } catch { }
            parts.Add(model + ", plate " + plate);

            try
            {
                var body = vehicle.Health;
                parts.Add(body >= 950 ? "clean, no visible damage" : body >= 700 ? "dented panels and scraped paint" : "badly damaged - crumpled bodywork");
                if (vehicle.EngineHealth < 400) parts.Add("smoke from under the hood");
                parts.Add(vehicle.IsEngineOn ? "engine running" : "engine off");
                var occupants = vehicle.Occupants;
                parts.Add(occupants == null || occupants.Length == 0 ? "nobody inside" : occupants.Length + " inside");
            }
            catch { }

            var record = _records.FindVehicle(plate);
            if (record != null && record.SearchFind != null) parts.Add("searched already: " + record.SearchFind);
            if (record != null && record.ReportedStolen && new Random(plate.GetHashCode()).Next(2) == 0) parts.Add("the ignition barrel is cracked");

            _chat.Do(string.Join("; ", parts.ToArray()) + ".");
            _chat.Me("You", "walk around the vehicle, looking it over.");
        }

        private static readonly string[] Agencies = { "LSSD", "SAHP", "LSFD", "San Andreas Park Rangers", "LSPD Air Support" };

        /// <summary>
        /// /d: the inter-department channel - for talking to the Sheriff, Highway Patrol or the fire
        /// department rather than your own dispatch. Somebody on it answers.
        /// </summary>
        private void DepartmentRadio(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument)) { _chat.Error("Usage: /d <message>   - the inter-department channel: LSSD, SAHP, LSFD."); return; }
            var text = argument.Trim();
            _chat.Write(ChatChannel.Radio, "[DEPT]", _dispatch.Unit + ": " + text);

            var lower = text.ToLowerInvariant();
            string agency = null;
            foreach (var name in Agencies)
                if (lower.Contains(name.ToLowerInvariant().Split(' ')[0])) { agency = name; break; }
            if (agency == null)
                agency = lower.Contains("fire") || lower.Contains("ems") || lower.Contains("medic") ? "LSFD"
                       : lower.Contains("highway") || lower.Contains("freeway") || lower.Contains("pursuit") ? "SAHP"
                       : lower.Contains("county") || lower.Contains("sandy") || lower.Contains("paleto") ? "LSSD"
                       : Agencies[new Random(Environment.TickCount).Next(2)];

            var rng = new Random(Environment.TickCount);
            var replies = new[]
            {
                "copies, " + _dispatch.Unit + ". Stand by.",
                "10-4, " + _dispatch.Unit + ", we'll keep an eye out.",
                "copy that. We've got a unit nearby if you need one.",
                "received, " + _dispatch.Unit + ". Advise if it crosses into our area.",
                "go ahead, " + _dispatch.Unit + ", we copy."
            };
            var reply = agency + " " + replies[rng.Next(replies.Length)];
            var delay = 1800 + rng.Next(2500);
            GameFiber.StartNew(() =>
            {
                GameFiber.Sleep(delay);
                _chat.Write(ChatChannel.Radio, "[DEPT]", reply);
            }, "TextDispatch dept radio");
        }

        /// <summary>/log: write the session out as a chat log - plain, and as BBCode for a forum post.</summary>
        private void ChatLogCommand(string argument)
        {
            var words = " " + (argument ?? "").Trim().ToLowerInvariant() + " ";
            if (words.Trim().Length == 0 || words.Contains(" help "))
            {
                _chat.Notice("Chat log: " + ChatLog.Count + " lines this session, " + ChatLog.Unexported + " not yet exported.");
                _chat.Notice("  /log export       the roleplay - speech, /me, /do, radio - as a chat log and as forum BBCode");
                _chat.Notice("  /log export all   everything, the box's own notices included");
                _chat.Notice("  /log export new   only what is new since the last export");
                _chat.Notice("  Written to " + ChatLog.Folder());
                return;
            }

            try
            {
                int written;
                var files = ChatLog.Export(words.Contains(" all "), words.Contains(" new ") || words.Contains(" since "), _dispatch.Unit, out written);
                _chat.Notice("Chat log written: " + written + " lines.");
                _chat.Notice("  " + files[0]);
                _chat.Notice("  " + files[1] + "  (paste into a forum post)");
                Log.Line("chat log: " + written + " lines to " + files[0]);
            }
            catch (Exception ex)
            {
                Log.Error("chat log export", ex);
                _chat.Error("Could not write the chat log: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ the penal code

        /// <summary>/charge [name:] &lt;charge&gt; - puts a section of the penal code on somebody's sheet.</summary>
        private void AddCharge(string argument)
        {
            var text = (argument ?? "").Trim();
            if (text.Length == 0)
            {
                _chat.Error("Usage: /charge <charge or section>   - on whoever is in front of you;  /charge Reyes: robbery  names them");
                _chat.Notice("  '/penal <word>' finds a section. '/arrest' then books everything on the sheet.");
                return;
            }

            PersonRecord person = null;
            var colon = text.IndexOf(':');
            if (colon > 0 && colon <= 32)
            {
                person = _records.FindPerson(text.Substring(0, colon).Trim());
                if (person == null) { _chat.Error("No record for '" + text.Substring(0, colon).Trim() + "'."); return; }
                text = text.Substring(colon + 1).Trim();
            }
            if (person == null) person = Subject(6f) != null ? PersonInFront() : _records.LastSubject;
            if (person == null) { _chat.Error("Who is it against? Stand in front of them, or '/charge <name>: <charge>'."); return; }

            var charge = PenalCode.Find(text);
            if (charge == null)
            {
                var close = PenalCode.Search(text);
                _chat.Error("No charge in the penal code matches '" + text + "'." + (close.Count > 0 ? " Did you mean:" : " '/penal' lists them."));
                for (var i = 0; i < close.Count && i < 5; i++) _chat.Notice("  " + close[i].Line());
                return;
            }

            if (person.PendingCharges.Contains(charge.Code)) { _chat.Notice(person.Name + " already has PC " + charge.Code + " on the sheet."); return; }
            person.PendingCharges.Add(charge.Code);
            _records.Touch(person);
            _records.Act(person, _dispatch.ReportCallName, "added PC " + charge.Code + " " + charge.Title.ToLowerInvariant() + " to their charge sheet");

            double fine; int days; string worst;
            PenalCode.Totals(person.PendingCharges, out fine, out days, out worst);
            _chat.Notice("Charge sheet, " + person.Name + ": + " + charge.Line());
            _chat.Notice("  " + person.PendingCharges.Count + " charge(s), $" + fine.ToString("0") + (days > 0 ? ", " + days + " days" : "") +
                         ", " + worst.ToLowerInvariant() + ". '/arrest' books them on it; '/charges clear' starts again.");
        }

        private void ListCharges(string argument)
        {
            var text = (argument ?? "").Trim();
            var clear = text.StartsWith("clear", StringComparison.OrdinalIgnoreCase);
            if (clear) text = text.Substring(5).Trim();

            var person = text.Length > 0 ? _records.FindPerson(text) : (Subject(6f) != null ? PersonInFront() : _records.LastSubject);
            if (person == null) { _chat.Error(text.Length > 0 ? "No record for '" + text + "'." : "Nobody in front of you, and no name given."); return; }

            if (clear)
            {
                person.PendingCharges.Clear();
                _records.Dirty = true;
                _chat.Notice("Charge sheet for " + person.Name + " cleared.");
                return;
            }

            var open = _records.OpenCase(person);
            if (person.PendingCharges.Count == 0 && (open == null || open.Charges.Count == 0))
            {
                _chat.Notice("No charges on " + person.Name + "'s sheet. '/charge <charge>' adds one.");
                return;
            }

            if (person.PendingCharges.Count > 0)
            {
                double fine; int days; string worst;
                PenalCode.Totals(person.PendingCharges, out fine, out days, out worst);
                _chat.Notice("Charge sheet, " + person.Name + " (not booked yet):");
                foreach (var code in person.PendingCharges) { var c = PenalCode.ByCode(code); _chat.Notice("  " + (c != null ? c.Line() : code)); }
                _chat.Notice("  Total $" + fine.ToString("0") + (days > 0 ? ", " + days + " days" : "") + ". '/arrest' books it.");
            }
            if (open != null && open.Charges.Count > 0)
                _chat.Notice("Booked, with the DA: " + PenalCode.Describe(open.Charges));
        }

        private void ListPenalCode(string argument)
        {
            var filter = (argument ?? "").Trim();
            var found = filter.Length == 0 ? PenalCode.All : PenalCode.Search(filter);
            if (found.Count == 0) { _chat.Notice("Nothing in the penal code matches '" + filter + "'."); return; }

            _chat.Notice("San Andreas penal code" + (filter.Length == 0 ? " - '/penal <word>' narrows it, e.g. /penal theft" : " matching '" + filter + "'") + ":");
            for (var i = 0; i < found.Count && i < 14; i++) _chat.Notice("  " + found[i].Line());
            if (found.Count > 14) _chat.Notice("  ... " + (found.Count - 14) + " more - narrow it with a word: /penal weapon, /penal felony, /penal 4-");
        }

        // ------------------------------------------------------------------ flags and notes

        /// <summary>/flag or /note &lt;name or plate&gt;: &lt;text&gt; - kept on the record for the next time they are run.</summary>
        private void FlagCommand(string argument, string kind)
        {
            var text = (argument ?? "").Trim();
            if (text.StartsWith("clear ", StringComparison.OrdinalIgnoreCase))
            {
                var who = text.Substring(6).Trim();
                var gone = _records.ClearFlags(who);
                _chat.Notice(gone > 0 ? gone + " flag(s) and note(s) cleared from " + who + "." : "Nothing on file for '" + who + "'.");
                return;
            }

            string subject, body;
            var colon = text.IndexOf(':');
            if (colon > 0)
            {
                subject = text.Substring(0, colon).Trim();
                body = text.Substring(colon + 1).Trim();
                var person = _records.FindPerson(subject);
                if (person != null) subject = person.Name;
            }
            else
            {
                // On whoever is in front of you, or the car you stopped.
                body = text;
                var ped = Subject(6f);
                var vehicle = _api.PulloverVehicle();
                subject = ped != null ? PersonInFront().Name : null;
                if (subject == null && vehicle != null) { try { subject = vehicle.LicensePlate.Trim(); } catch { } }
            }

            if (string.IsNullOrEmpty(subject) || body.Length == 0)
            {
                _chat.Error("Usage: /" + kind + " <name or plate>: <text>   or   /" + kind + " <text> for whoever is in front of you");
                _chat.Notice(kind == "flag" ? "  A flag is a warning - 'known to carry a knife' - and dispatch reads it out when they are stopped."
                                            : "  A note is for the record - 'works nights at the Bean Machine'. /notes <name> lists both.");
                return;
            }

            _records.AddFlag(subject, kind, body);
            _chat.Notice((kind == "flag" ? "FLAG" : "Note") + " on " + subject + ": " + body +
                         (kind == "flag" ? " - read out when they or the plate are stopped or run." : ""));
        }

        private void ListFlags(string argument)
        {
            var subject = (argument ?? "").Trim();
            if (subject.Length == 0)
            {
                if (_records.Flags.Count == 0) { _chat.Notice("No flags or notes on file. '/flag <name or plate>: <text>' adds one."); return; }
                _chat.Notice("Flags and notes, newest first:");
                for (int i = _records.Flags.Count - 1, shown = 0; i >= 0 && shown < 10; i--, shown++)
                {
                    var flag = _records.Flags[i];
                    _chat.Notice("  " + (flag.Kind == "flag" ? "FLAG " : "note ") + flag.Subject + " - " + flag.Text + "  (" + flag.When + ")");
                }
                return;
            }

            var person = _records.FindPerson(subject);
            if (!ShowFlags(person != null ? person.Name : subject))
                _chat.Notice("No flags or notes on '" + subject + "'.");
        }

        /// <summary>The flags and notes on a name or plate, and any BOLO; false when there is nothing.</summary>
        private bool ShowFlags(string subject)
        {
            var any = false;
            foreach (var flag in _records.FlagsOn(subject))
            {
                _chat.Notice("  " + (flag.Kind == "flag" ? "FLAG: " : "note: ") + flag.Text + "  (" + flag.When + ")");
                any = true;
            }
            var bolo = _records.BoloFor(subject);
            if (bolo != null) { _chat.Notice("  BOLO: " + bolo.Reason); any = true; }
            return any;
        }

        // ------------------------------------------------------------------ investigations

        private void CaseCommand(string argument)
        {
            var text = (argument ?? "").Trim();
            var words = text.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            var verb = words.Length > 0 ? words[0].ToLowerInvariant() : "";

            if (verb.Length == 0 || verb == "list")
            {
                if (_records.Cases.Count == 0) { _chat.Notice("No investigations. '/case open <title>' starts one, and reports go under it."); return; }
                _chat.Notice("Investigations" + (_records.ActiveCase != 0 ? " - working #" + _records.ActiveCase : "") + ":");
                for (int i = _records.Cases.Count - 1, shown = 0; i >= 0 && shown < 8; i--, shown++)
                {
                    var file = _records.Cases[i];
                    _chat.Notice("  #" + file.Number + "  " + file.Title + "  -  " + (file.Closed == null ? "open" : "closed (" + file.Outcome + ")") +
                                 ", " + file.Reports.Count + " report(s), " + file.People.Count + " name(s)");
                }
                _chat.Notice("  /case <n>  /case use <n>  /case add <n> <name>  /case note <n> <text>  /case close <n> <outcome>  /case export <n>");
                return;
            }

            if (verb == "open" || verb == "new")
            {
                var title = text.Substring(words[0].Length).Trim();
                var file = _records.OpenInvestigation(title);
                _chat.Notice("Case #" + file.Number + " opened: " + file.Title + ". Reports you file now go under it ('/case use 0' stops that).");
                return;
            }

            // Everything else names a case: "/case 3", "/case use 3", "/case note 3 text".
            int number;
            if (int.TryParse(verb.TrimStart('#'), out number)) { ShowCase(number); return; }
            if (words.Length < 2 || !int.TryParse(words[1].TrimStart('#'), out number))
            {
                _chat.Error("Which case? /case " + verb + " <number> ...  - /case lists them.");
                return;
            }
            var rest = words.Length > 2 ? words[2].Trim() : "";

            if (verb == "use")
            {
                if (number == 0) { _records.UseCase(0); _chat.Notice("Reports go under no case now."); return; }
                var target = _records.FindCase(number);
                if (target == null || target.Closed != null) { _chat.Error("No open case #" + number + "."); return; }
                _records.UseCase(number);
                _chat.Notice("Working case #" + number + " - " + target.Title + ". Reports you file go under it.");
                return;
            }

            var file2 = _records.FindCase(number);
            if (file2 == null) { _chat.Error("No case #" + number + ". /case lists them."); return; }

            switch (verb)
            {
                case "add":
                {
                    var person = rest.Length > 0 ? _records.FindPerson(rest) : (Subject(6f) != null ? PersonInFront() : _records.LastSubject);
                    if (person == null) { _chat.Error("No record for '" + rest + "'."); return; }
                    _records.AddToCase(file2, person);
                    _chat.Notice(person.Name + " added to case #" + number + ".");
                    return;
                }
                case "note":
                    if (rest.Length == 0) { _chat.Error("Usage: /case note " + number + " <text>"); return; }
                    _records.NoteOnCase(file2, rest);
                    _chat.Notice("Noted on case #" + number + ".");
                    return;
                case "close":
                    _records.CloseCase(file2, rest);
                    _chat.Notice("Case #" + number + " closed: " + file2.Outcome + ". '/case export " + number + "' writes it up for the forum.");
                    return;
                case "export":
                    try
                    {
                        var path = ReportForms.ExportCase(_records, file2, Officer(), Settings.PluginFolder());
                        _chat.Notice("Case #" + number + " written as a forum post: " + path);
                    }
                    catch (Exception ex) { Log.Error("case export", ex); _chat.Error("Could not write it: " + ex.Message); }
                    return;
                default:
                    _chat.Error("'/case " + verb + "' is not something a case does. /case lists the options.");
                    return;
            }
        }

        private void ShowCase(int number)
        {
            var file = _records.FindCase(number);
            if (file == null) { _chat.Error("No case #" + number + "."); return; }

            _chat.Notice("Case #" + file.Number + " - " + file.Title + "  (" + (file.Closed == null ? "open since " + file.Opened : "closed " + file.Closed + ": " + file.Outcome) + ")");
            _chat.Notice("  People: " + (file.People.Count == 0 ? "none yet" : string.Join(", ", file.People.ToArray())));
            foreach (var reportNumber in file.Reports)
            {
                var report = _records.FindReport(reportNumber);
                if (report == null) continue;
                var body = report.Text.Length > 90 ? report.Text.Substring(0, 87) + "..." : report.Text;
                _chat.Notice("  report #" + report.Number + " " + report.When + ": " + body);
            }
            for (var i = Math.Max(0, file.Notes.Count - 5); i < file.Notes.Count; i++) _chat.Notice("  note " + file.Notes[i]);
        }

        // ------------------------------------------------------------------ reports as forum posts

        private void ExportReport(string which)
        {
            ReportRecord report = null;
            int number;
            if (which.Length == 0 || which.Equals("last", StringComparison.OrdinalIgnoreCase))
                report = _records.Reports.Count > 0 ? _records.Reports[_records.Reports.Count - 1] : null;
            else if (int.TryParse(which.TrimStart('#'), out number))
                report = _records.FindReport(number);

            if (report == null) { _chat.Error(which.Length == 0 ? "No reports on file yet." : "No report " + which + ". /reports lists them."); return; }

            try
            {
                var path = ReportForms.Export(_records, report, Officer(), Settings.PluginFolder());
                _chat.Notice("Report #" + report.Number + " written as a forum post (BBCode, with a plain copy under it):");
                _chat.Notice("  " + path);
            }
            catch (Exception ex) { Log.Error("report export", ex); _chat.Error("Could not write it: " + ex.Message); }
        }

        /// <summary>/report fix &lt;n&gt; &lt;more&gt; - adds to a report the sergeant sent back, and sends it again.</summary>
        private void FixReport(string text)
        {
            var split = text.IndexOf(' ');
            int number;
            if (split < 0 || !int.TryParse(text.Substring(0, split).TrimStart('#'), out number))
            {
                var returned = _records.ReturnedReports();
                _chat.Error("Usage: /report fix <number> <what to add>");
                foreach (var r in returned) _chat.Notice("  #" + r.Number + " was sent back: " + r.ReviewNotes);
                return;
            }

            var report = _records.FindReport(number);
            if (report == null) { _chat.Error("No report #" + number + "."); return; }
            _records.AmendReport(report, text.Substring(split + 1));
            _chat.Me("You", "add to the report and send it back up.");
            _chat.Notice("Report #" + number + " amended and back with the sergeant.");
        }

        /// <summary>Who signs the paperwork: the rank and the callsign.</summary>
        private string Officer()
        {
            return Career.RankOf(_records, _shift.Current).Title + ", " + _dispatch.Unit;
        }

        // ------------------------------------------------------------------ the career

        private void ShowRank()
        {
            var rank = Career.RankOf(_records, _shift.Current);
            var totals = Career.Count(_records, _shift.Current);
            _chat.Notice(rank.Title + " - " + rank.Points + " points" + (rank.Next != null ? ", " + rank.ToNext + " to " + rank.Next : ", the top of the ladder") + ".");
            _chat.Notice("  " + totals.Shifts + " shift(s), " + totals.Calls + " call(s), " + totals.Arrests + " arrest(s) - " + _records.ChargedCount +
                         " charged, " + totals.Citations + " citation(s), " + _records.ReportsApproved + " report(s) approved, " +
                         _records.ReportsReturned + " sent back, " + _records.CasesClosed + " case(s) closed.");
            _chat.Notice("  Points: shift 5, call 2, arrest 3, charged 4, approved report 4, closed case 8, find 2, pursuit 2, citation 1; a report sent back -2.");
            _chat.Notice("  /certs - the specialist certifications.");
        }

        private void ShowCertifications()
        {
            _chat.Notice("Certifications" + (_settings.RequireCertifications ? " (required for /k9 and air support)" : " - RequireCertifications=0, so nothing is locked") + ":");
            foreach (var cert in Career.Certifications(_records, _shift.Current))
                _chat.Notice("  " + (cert.Held ? "[x] " : "[ ] ") + cert.Name.PadRight(24) + (cert.Held ? "certified" : "needs " + cert.Progress));
        }

        /// <summary>Whether this can be used: always, unless the ini asks for certifications and it is not held.</summary>
        private bool Certified(string key, string name)
        {
            if (!_settings.RequireCertifications || Career.Holds(_records, key, _shift.Current)) return true;
            _chat.Error("You are not " + name + " certified yet. /certs says what it takes.");
            return false;
        }

        // ------------------------------------------------------------------ the phone

        private void PhoneCommand(string argument)
        {
            if (Phone == null) { _chat.Error("The phone is not running."); return; }
            var text = (argument ?? "").Trim();

            if (text.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
            {
                var person = _records.FindPerson(text.Substring(4).Trim());
                if (person == null) { _chat.Error("No record for '" + text.Substring(4).Trim() + "'."); return; }
                Phone.Save(person.Name);
                _chat.Notice(person.Name + " saved in your phone.");
                return;
            }

            _chat.Notice("Phone - '/text <contact> <message>', '/call <contact> <what you say>', '/inbox [contact]':");
            foreach (var contact in Phone.Contacts())
            {
                var role = PhoneService.RoleOf(contact);
                var person = role == null ? _records.FindPerson(contact) : null;
                _chat.Phone("", "  " + contact + "  -  " + (role ?? (person != null ? person.Summary() : "")));
            }
            _chat.Notice("  Anybody you have met has given you their number; '/phone add <name>' saves them.");
        }

        private void TextCommand(string argument, bool call)
        {
            if (Phone == null) { _chat.Error("The phone is not running."); return; }
            var text = (argument ?? "").Trim();
            var usage = call ? "Usage: /call <contact> <what you say>   e.g. /call Ramirez any news on the Reyes case?"
                             : "Usage: /text <contact> <message>   e.g. /text Lucky who's been selling in Davis?";
            if (text.Length == 0) { _chat.Error(usage); return; }

            // The contact is the longest start of the line that is somebody in the phone.
            string contact = null, message = null;
            PersonRecord person = null;
            var words = text.Split(' ');
            for (var n = Math.Min(4, words.Length - 1); n >= 1 && contact == null; n--)
            {
                var name = string.Join(" ", words, 0, n);
                var found = Phone.Resolve(name, out person);
                if (found != null) { contact = found; message = string.Join(" ", words, n, words.Length - n); }
            }
            if (contact == null)
            {
                _chat.Error("Nobody in your phone matches '" + words[0] + "'. /phone lists your contacts.");
                return;
            }
            if (string.IsNullOrWhiteSpace(message)) { _chat.Error(usage); return; }

            if (call) _chat.Me("You", "call " + contact + " on your phone.");
            Phone.Text(contact, person, message.Trim(), call);
        }

        private void Inbox(string argument)
        {
            if (Phone == null) { _chat.Error("The phone is not running."); return; }
            var text = (argument ?? "").Trim();
            PersonRecord person;
            Phone.Inbox(text.Length == 0 ? null : Phone.Resolve(text, out person) ?? text);
        }

        // ------------------------------------------------------------------ the radar

        private void RadarCommand(string argument)
        {
            if (Radar == null) { _chat.Error("The radar is not running."); return; }
            var words = (argument ?? "").Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var verb = words.Length > 0 ? words[0] : "";

            // Which antenna a command is about: "front", "rear" (or "back"), or both when neither is said.
            var which = words.Length > 1 ? words[1] : "";
            var both = which == "" || which == "both";
            var front = both || which.StartsWith("f");
            var rear = both || which.StartsWith("r") || which == "back";

            switch (verb)
            {
                case "on":
                    if (front) Radar.FrontOn = true;
                    if (rear) Radar.RearOn = true;
                    _chat.Notice("Radar on - " + Radar.Watching + " antenna" + (Radar.FrontOn && Radar.RearOn ? "s" : "") +
                                 " watching, calling out anything over " + Radar.AlertMph + " mph. '/radar alert <mph>' changes it.");
                    return;
                case "off":
                    if (front) Radar.FrontOn = false;
                    if (rear) Radar.RearOn = false;
                    _chat.Notice(Radar.On ? "Radar: only the " + Radar.Watching + " antenna is watching now." : "Radar off.");
                    return;
                case "alert":
                case "limit":
                    int mph;
                    if (words.Length < 2 || !int.TryParse(words[1], out mph) || mph < 10 || mph > 250) { _chat.Error("Usage: /radar alert <mph>   e.g. /radar alert 70"); return; }
                    Radar.AlertMph = mph;
                    _chat.Notice("Radar alert set to " + mph + " mph.");
                    return;
                case "clear":
                case "unlock":
                    Radar.Locked = null;
                    _chat.Notice("Radar lock cleared.");
                    return;
            }

            Radar.Reading frontTarget, rearTarget;
            int patrol;
            if (!Radar.Scan(out frontTarget, out rearTarget, out patrol)) { _chat.Error("The radar is in the car - get in one first."); return; }

            // "/radar front" or "/radar rear" is that antenna's reading on its own.
            if (verb == "front" || verb == "rear" || verb == "back")
            {
                var reading = verb == "front" ? frontTarget : rearTarget;
                _chat.Write(ChatChannel.Radio, "[RADAR]", "Patrol " + patrol + " mph  |  " +
                            (reading != null ? reading.Describe() : (verb == "front" ? "FRONT" : "REAR") + " no target"));
                return;
            }

            if (verb == "lock")
            {
                // "/radar lock rear" locks the rear target; with no antenna named, the front, then the rear.
                var target = both ? frontTarget ?? rearTarget : front ? frontTarget : rearTarget;
                if (target == null) { _chat.Error(both ? "Nothing in either beam to lock." : "Nothing in the " + (front ? "front" : "rear") + " beam to lock."); return; }
                Radar.Locked = target;
                _chat.Write(ChatChannel.Radio, "[RADAR]", "LOCKED: " + target.Describe() + ". /cite speeding uses it.");
                return;
            }

            _chat.Write(ChatChannel.Radio, "[RADAR]", "Patrol " + patrol + " mph  |  " + (frontTarget != null ? frontTarget.Describe() : "FRONT no target") +
                                                       "  |  " + (rearTarget != null ? rearTarget.Describe() : "REAR no target"));
            _chat.Notice("  Watching: " + Radar.Watching + ", alert " + Radar.AlertMph + " mph." +
                         (Radar.Locked != null ? "  Locked: " + Radar.Locked.Describe() + " at " + Radar.Locked.At.ToString("HH:mm:ss") + "." : ""));
            _chat.Notice("  /radar front|rear   /radar lock [front|rear]   /radar on|off [front|rear]   /radar alert <mph>");
        }

        /// <summary>
        /// The radar lock that belongs on a speeding ticket: the car you stopped, locked in the last ten
        /// minutes - or null.
        /// </summary>
        private Radar.Reading LockFor(Vehicle stopped)
        {
            if (Radar == null || Radar.Locked == null || (DateTime.Now - Radar.Locked.At).TotalMinutes > 10) return null;
            if (stopped == null) return Radar.Locked;
            try
            {
                if (Radar.Locked.Vehicle != null && Radar.Locked.Vehicle.Exists() && Radar.Locked.Vehicle == stopped) return Radar.Locked;
                return string.Equals(Radar.Locked.Plate, stopped.LicensePlate.Trim(), StringComparison.OrdinalIgnoreCase) ? Radar.Locked : null;
            }
            catch { return null; }
        }
    }
}
