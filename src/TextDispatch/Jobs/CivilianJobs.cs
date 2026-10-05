using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace TextDispatch.Jobs
{
    /// <summary>One civilian job, exactly as DriverJobs V writes it down.</summary>
    internal sealed class CivilianJob
    {
        public string Name = "";
        public string Kind = "";
        public bool Remote;
        public int PayBase;
        public float PayPerUnit;
        public string CompanyVehicle;
        public string LicencePlate;
        public string OwnVehicle;
        public string[] Lines = new string[0];
        public bool HasUniform;
        public float X, Y, Z;
        public bool HasStart;

        /// <summary>'ParamedicMissionModel' reads as 'paramedic' to somebody standing in the street.</summary>
        public string KindText()
        {
            var kind = Kind ?? "";
            if (kind.EndsWith("MissionModel", StringComparison.OrdinalIgnoreCase))
                kind = kind.Substring(0, kind.Length - "MissionModel".Length);
            if (kind.Length == 0) return "civilian work";

            var words = new List<string>();
            var current = "";
            foreach (var c in kind)
            {
                if (char.IsUpper(c) && current.Length > 0) { words.Add(current); current = ""; }
                current += c;
            }
            if (current.Length > 0) words.Add(current);
            return string.Join(" ", words.ToArray()).ToLowerInvariant();
        }

        public string PayText()
        {
            var text = "pays " + PayBase.ToString(CultureInfo.InvariantCulture);
            if (PayPerUnit > 0f)
                text += ", plus " + PayPerUnit.ToString("0.###", CultureInfo.InvariantCulture) + " a unit";
            return text;
        }

        public string VehicleText()
        {
            if (!string.IsNullOrEmpty(CompanyVehicle))
                return "the company " + CompanyVehicle + (string.IsNullOrEmpty(LicencePlate) ? "" : " (" + LicencePlate + ")");
            if (!string.IsNullOrEmpty(OwnVehicle) && OwnVehicle != "None")
                return "your own vehicle, of the " + OwnVehicle + " kind";
            if (!string.IsNullOrEmpty(OwnVehicle))
                return "the company vehicle only - your own car will not do";
            return "a vehicle of the job's own";
        }

        /// <summary>One line for the log, which is where a list of 37 belongs.</summary>
        public string LogLine()
        {
            var where = HasStart
                ? string.Format(CultureInfo.InvariantCulture, "starts at {0:0.#},{1:0.#},{2:0.#}", X, Y, Z)
                : "no start point in the file";
            return Name + "  [" + Kind + "]" + (Remote ? " (startable from the menu)" : "") + "  " + PayText() +
                   "  " + VehicleText() + "  " + where + (HasUniform ? "  (work uniform)" : "");
        }
    }

    /// <summary>
    /// DriverJobs V's civilian jobs, read out of the file the mod itself works from.
    ///
    /// There is no API to ask and no callout registration to hang a question on - DriverJobs is a
    /// ScriptHookV script - so the list is taken from `scripts\DriverJobsData\Missions\Jobs.xml`,
    /// which is the same file the mod loads at startup. Two things follow from reading the file
    /// rather than a copy of it: what the box shows is what the mod actually has, including a job
    /// edited or added by hand; and nothing here has a RAGE type in it, so the parsing can be
    /// tested outside the game.
    ///
    /// Not installed is a normal state, not a fault: a missing file is one sentence in the box.
    /// </summary>
    internal static class CivilianJobs
    {
        private static string _location;
        private static string _problem = "the job list has not been looked for yet";
        private static List<CivilianJob> _jobs;
        private static DateTime _stamp;
        private static long _size;

        /// <summary>The file it found, for saying where the list came from.</summary>
        public static string Location
        {
            get { Load(); return _location; }
        }

        /// <summary>Why there is nothing to show, when there is nothing to show.</summary>
        public static string Problem
        {
            get { Load(); return _problem; }
        }

        public static IList<CivilianJob> All()
        {
            Load();
            return _jobs ?? (IList<CivilianJob>)new List<CivilianJob>();
        }

        /// <summary>
        /// Work out where the job list is, from where this plugin is installed: DriverJobs lives in
        /// the game folder's scripts directory, two levels up from Plugins\LSPDFR.
        /// </summary>
        public static void Locate(string pluginFolder)
        {
            if (string.IsNullOrEmpty(pluginFolder)) { _problem = "the plugin folder could not be worked out"; return; }

            foreach (var candidate in Candidates(pluginFolder))
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    if (string.Equals(candidate, _location, StringComparison.OrdinalIgnoreCase)) return;
                    _location = candidate;
                    _jobs = null;
                    _stamp = DateTime.MinValue;
                    Log.Line("jobs: reading " + candidate);
                    return;
                }
                catch { }
            }

            if (_location == null)
                _problem = "no file at " + Path.Combine(GameFolder(pluginFolder), "scripts\\DriverJobsData\\Missions\\Jobs.xml");
        }

        private static IEnumerable<string> Candidates(string pluginFolder)
        {
            var game = GameFolder(pluginFolder);
            var relative = Path.Combine("scripts", "DriverJobsData", "Missions", "Jobs.xml");

            yield return Path.Combine(game, relative);
            yield return Path.Combine(pluginFolder, "..", "..", relative);
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", relative);
            yield return Path.Combine(Environment.CurrentDirectory, relative);
        }

        private static string GameFolder(string pluginFolder)
        {
            try
            {
                // Plugins\LSPDFR -> the folder holding GTA5.exe
                var parent = Directory.GetParent(pluginFolder);
                if (parent != null && parent.Parent != null) return parent.Parent.FullName;
            }
            catch { }
            return Environment.CurrentDirectory;
        }

        /// <summary>
        /// Find a job by number in the list, by its name, or by part of its name.
        /// </summary>
        public static CivilianJob Find(string what, out string problem)
        {
            problem = null;
            var jobs = All();
            if (jobs.Count == 0) { problem = Problem; return null; }

            var text = (what ?? "").Trim();
            if (text.Length == 0) { problem = "no name was given"; return null; }

            int number;
            if (int.TryParse(text, out number) && number >= 1 && number <= jobs.Count) return jobs[number - 1];

            foreach (var job in jobs)
                if (string.Equals(job.Name, text, StringComparison.OrdinalIgnoreCase)) return job;

            var matches = new List<CivilianJob>();
            foreach (var job in jobs)
                if (job.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) matches.Add(job);

            if (matches.Count == 1) return matches[0];
            if (matches.Count == 0) { problem = "no job is called that"; return null; }

            // Numbered, because the file really does hold two jobs with the same name - the two
            // freight runs - and "2 jobs match" is not something a player can act on.
            problem = matches.Count + " jobs match";
            foreach (var match in matches)
            {
                var index = jobs.IndexOf(match) + 1;
                problem += " - " + index + ". " + match.Name;
            }
            return null;
        }

        /// <summary>
        /// Read the file, and re-read it when it changes - so a job edited by hand is picked up
        /// without restarting the game.
        /// </summary>
        private static void Load()
        {
            if (_location == null || !File.Exists(_location)) return;

            try
            {
                var info = new FileInfo(_location);
                if (_jobs != null && info.LastWriteTimeUtc == _stamp && info.Length == _size) return;

                var document = new XmlDocument();
                document.Load(_location);

                if (document.DocumentElement == null) { _problem = "the job list is empty"; return; }

                var jobs = new List<CivilianJob>();
                foreach (XmlNode node in document.DocumentElement.ChildNodes)
                {
                    if (node.NodeType != XmlNodeType.Element || node.Name != "mission") continue;
                    var job = Read(node);
                    if (job != null) jobs.Add(job);
                }

                _jobs = jobs;
                _stamp = info.LastWriteTimeUtc;
                _size = info.Length;
                _problem = jobs.Count == 0 ? "the job list holds nothing" : null;
            }
            catch (Exception ex)
            {
                _jobs = null;
                _problem = ex.Message;
                Log.Error("jobs", ex);
            }
        }

        private static CivilianJob Read(XmlNode node)
        {
            try
            {
                var job = new CivilianJob();
                job.Name = Attribute(node, "name") ?? "(unnamed)";
                job.Kind = Attribute(node, "type") ?? "";
                job.Remote = Truthy(Attribute(node, "remote"));

                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.NodeType != XmlNodeType.Element) continue;

                    switch (child.Name)
                    {
                        case "about":
                            job.Lines = Lines(child);
                            break;

                        case "start":
                            job.X = Number(child, "x");
                            job.Y = Number(child, "y");
                            job.Z = Number(child, "z");
                            job.HasStart = true;
                            break;

                        case "salary":
                            job.PayBase = (int)Number(child, "base");
                            job.PayPerUnit = Number(child, "perUnit");
                            break;

                        case "personalVehicle":
                            job.OwnVehicle = Attribute(child, "category");
                            break;

                        case "companyVehicles":
                            foreach (XmlNode vehicle in child.ChildNodes)
                            {
                                if (vehicle.NodeType != XmlNodeType.Element || vehicle.Name != "vehicle") continue;
                                if (string.IsNullOrEmpty(job.CompanyVehicle)) job.CompanyVehicle = Attribute(vehicle, "model");
                                if (string.IsNullOrEmpty(job.LicencePlate)) job.LicencePlate = Attribute(vehicle, "licensePlate");
                            }
                            break;

                        case "skins":
                            foreach (XmlNode skin in child.ChildNodes)
                                if (skin.NodeType == XmlNodeType.Element && skin.Name == "skin" &&
                                    !string.IsNullOrEmpty(skin.InnerText.Trim())) job.HasUniform = true;
                            break;
                    }
                }

                return job;
            }
            catch (Exception ex)
            {
                Log.Error("job entry", ex);
                return null;
            }
        }

        private static string[] Lines(XmlNode about)
        {
            var lines = new List<string>();
            foreach (XmlNode line in about.ChildNodes)
            {
                if (line.NodeType != XmlNodeType.Element || line.Name != "line") continue;
                lines.Add(Plain(line.InnerText));
            }
            return lines.ToArray();
        }

        /// <summary>
        /// The mod's own colour codes, and its markup, out of a line meant for its UI rather than for
        /// a chat box: 'Central LS Medical Center' should read as that, not as '~g~Central...'.
        /// </summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var result = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '~' && i + 2 < text.Length && text[i + 2] == '~') { i += 2; continue; }
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i);
                    if (close > i) { i = close; continue; }
                }
                result.Append(text[i]);
            }
            return result.ToString().Trim();
        }

        private static string Attribute(XmlNode node, string localName)
        {
            if (node.Attributes == null) return null;
            foreach (XmlAttribute attribute in node.Attributes)
            {
                if (string.Equals(attribute.LocalName, localName, StringComparison.OrdinalIgnoreCase)) return attribute.Value;
            }
            return null;
        }

        private static float Number(XmlNode node, string localName)
        {
            var text = Attribute(node, localName);
            float value;
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0f;
        }

        private static bool Truthy(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var text = value.Trim();
            return text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
