using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TextDispatch.Records
{
    /// <summary>
    /// Reports and cases as forum posts.
    ///
    /// On GTA World an arrest report is a forum thread in a fixed layout - the officer, the suspect, the
    /// charges by section, the evidence, the narrative - written in BBCode. This writes a report on file
    /// in that layout, so it can be pasted straight into a post, along with a plain-text copy for anywhere
    /// that does not read BBCode.
    /// </summary>
    public static class ReportForms
    {
        public static string Folder(string pluginFolder)
        {
            return Path.Combine(pluginFolder ?? "", "TextDispatch Reports");
        }

        /// <summary>Write one report out. Returns the file written.</summary>
        public static string Export(RecordsLedger ledger, ReportRecord report, string officer, string pluginFolder)
        {
            var person = report.Subject == null ? null : ledger.FindPerson(report.Subject);
            var open = person == null ? null : ledger.OpenCase(person);
            var arrest = report.Charges.Count > 0 || open != null;
            var kind = arrest ? "ARREST REPORT" : person != null ? "INCIDENT REPORT" : "GENERAL REPORT";

            var bb = new StringBuilder();
            bb.AppendLine("[center][b][size=150]LOS SANTOS POLICE DEPARTMENT[/size][/b]");
            bb.AppendLine("[b]" + kind + " #" + report.Number + "[/b][/center]");
            bb.AppendLine("[hr]");
            bb.AppendLine("[b]SECTION I - GENERAL INFORMATION[/b]");
            bb.AppendLine("[list]");
            bb.AppendLine("[*][b]Reporting officer:[/b] " + Or(officer, report.Unit));
            bb.AppendLine("[*][b]Unit:[/b] " + Or(report.Unit, "-"));
            bb.AppendLine("[*][b]Date and time:[/b] " + Or(report.When, "-"));
            bb.AppendLine("[*][b]Call:[/b] " + Or(report.Call, "officer initiated") + (report.Incident != null ? " - incident #" + report.Incident : ""));
            if (report.Case != 0)
            {
                var file = ledger.FindCase(report.Case);
                bb.AppendLine("[*][b]Investigation:[/b] #" + report.Case + (file != null ? " - " + file.Title : ""));
            }
            bb.AppendLine("[/list]");

            if (person != null)
            {
                bb.AppendLine("[b]SECTION II - SUBJECT[/b]");
                bb.AppendLine("[list]");
                bb.AppendLine("[*][b]Name:[/b] " + person.Name);
                bb.AppendLine("[*][b]Age:[/b] " + person.Age);
                bb.AppendLine("[*][b]Occupation:[/b] " + Or(person.Occupation, "unknown"));
                bb.AppendLine("[*][b]Residence:[/b] " + Or(person.HomeZone, "unknown"));
                bb.AppendLine("[*][b]Prior record:[/b] " + (person.Priors.Count == 0 ? "none on file" : string.Join(", ", person.Priors.ToArray())));
                bb.AppendLine("[/list]");
            }

            if (report.Charges.Count > 0)
            {
                bb.AppendLine("[b]SECTION III - CHARGES[/b]");
                bb.AppendLine("[list]");
                foreach (var code in report.Charges)
                {
                    var charge = PenalCode.ByCode(code);
                    bb.AppendLine("[*]" + (charge == null ? code : "PC " + charge.Code + " - " + charge.Title + " (" + charge.Class + ")"));
                }
                double fine; int days; string worst;
                PenalCode.Totals(report.Charges, out fine, out days, out worst);
                bb.AppendLine("[/list]");
                bb.AppendLine("[i]Schedule: $" + fine.ToString("0") + (days > 0 ? ", " + days + " days" : "") + ", highest class " + Or(worst, "-") + ".[/i]");
            }

            if (person != null)
            {
                var evidence = ledger.EvidenceOn(person);
                bb.AppendLine("[b]SECTION IV - EVIDENCE[/b]");
                if (evidence.Count == 0) bb.AppendLine("None logged.");
                else
                {
                    bb.AppendLine("[list]");
                    foreach (var item in evidence) bb.AppendLine("[*]" + item);
                    bb.AppendLine("[/list]");
                }

                var statement = open != null ? open.Statement : person.Statement;
                var mirandized = open != null ? open.Mirandized : person.StatementMirandized;
                bb.AppendLine("[b]Statement:[/b] " + (statement == null ? "none taken" : RecordsLedger.Describe(statement) +
                              (mirandized ? ", after Miranda" : ", WITHOUT Miranda")));
            }

            bb.AppendLine("[b]SECTION V - NARRATIVE[/b]");
            bb.AppendLine("[quote]" + report.Text + "[/quote]");

            if (report.Review != null)
                bb.AppendLine("[size=85][i]Supervisor review: " + report.Review + (report.ReviewNotes != null ? " - " + report.ReviewNotes : "") + "[/i][/size]");

            bb.AppendLine("[hr]");
            bb.AppendLine("[i]Signed, " + Or(officer, report.Unit) + "[/i]");

            return Write(pluginFolder, "report " + report.Number + (person != null ? " - " + person.Name : ""), bb.ToString());
        }

        /// <summary>A whole investigation: the summary, the people, the notes, and every report under it.</summary>
        public static string ExportCase(RecordsLedger ledger, CaseFile file, string officer, string pluginFolder)
        {
            var bb = new StringBuilder();
            bb.AppendLine("[center][b][size=150]LOS SANTOS POLICE DEPARTMENT[/size][/b]");
            bb.AppendLine("[b]INVESTIGATION #" + file.Number + " - " + file.Title.ToUpperInvariant() + "[/b][/center]");
            bb.AppendLine("[hr]");
            bb.AppendLine("[list]");
            bb.AppendLine("[*][b]Lead officer:[/b] " + Or(officer, "-"));
            bb.AppendLine("[*][b]Opened:[/b] " + file.Opened);
            bb.AppendLine("[*][b]Status:[/b] " + (file.Closed == null ? "OPEN" : "CLOSED " + file.Closed + " - " + file.Outcome));
            bb.AppendLine("[/list]");

            bb.AppendLine("[b]PERSONS OF INTEREST[/b]");
            if (file.People.Count == 0) bb.AppendLine("None yet.");
            else
            {
                bb.AppendLine("[list]");
                foreach (var name in file.People)
                {
                    var person = ledger.FindPerson(name);
                    bb.AppendLine("[*]" + name + (person != null ? " - " + person.Age + ", " + person.Summary() : ""));
                }
                bb.AppendLine("[/list]");
            }

            bb.AppendLine("[b]CASE NOTES[/b]");
            if (file.Notes.Count == 0) bb.AppendLine("None.");
            else
            {
                bb.AppendLine("[list]");
                foreach (var note in file.Notes) bb.AppendLine("[*]" + note);
                bb.AppendLine("[/list]");
            }

            bb.AppendLine("[b]REPORTS[/b]");
            if (file.Reports.Count == 0) bb.AppendLine("None filed under this case.");
            foreach (var number in file.Reports)
            {
                var report = ledger.FindReport(number);
                if (report == null) continue;
                bb.AppendLine("[spoiler=Report #" + report.Number + " - " + report.When + " - " + Or(report.Subject, "general") + "]" +
                              report.Text + "[/spoiler]");
            }

            return Write(pluginFolder, "case " + file.Number + " - " + file.Title, bb.ToString());
        }

        private static string Write(string pluginFolder, string name, string bbcode)
        {
            var folder = Folder(pluginFolder);
            Directory.CreateDirectory(folder);
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
            var path = Path.Combine(folder, name + ".txt");

            var text = new StringBuilder();
            text.AppendLine("; Paste everything below this line into a forum post - it is BBCode.");
            text.Append(bbcode);
            text.AppendLine();
            text.AppendLine("; ---- The same, as plain text ----");
            text.Append(Plain(bbcode));
            File.WriteAllText(path, text.ToString(), Encoding.UTF8);
            return path;
        }

        /// <summary>BBCode with the tags taken out, and list items as dashes.</summary>
        public static string Plain(string bbcode)
        {
            var text = (bbcode ?? "").Replace("[*]", "  - ").Replace("[hr]", new string('-', 60));
            var clean = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    var end = text.IndexOf(']', i);
                    if (end > i && end - i < 40 && IsTag(text.Substring(i + 1, end - i - 1))) { i = end; continue; }
                }
                clean.Append(text[i]);
            }
            return clean.ToString();
        }

        private static bool IsTag(string inside)
        {
            var name = inside.TrimStart('/').Split('=')[0].ToLowerInvariant();
            switch (name)
            {
                case "b": case "i": case "u": case "center": case "size": case "list": case "quote": case "spoiler": case "hr":
                case "color": case "font": case "background":
                    return true;
                default:
                    return false;
            }
        }

        private static string Or(string text, string fallback)
        {
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
    }
}
