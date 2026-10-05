using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TextDispatch;

namespace TextJobs
{
    /// <summary>
    /// TextJobs' settings, from TextJobs.ini beside the script.
    ///
    /// Small on purpose: this plugin is a chat box for somebody else's mod, so what is worth choosing
    /// is the key, where the box sits, how big it is, and whether typing is hidden from the other
    /// plugins.
    /// </summary>
    internal sealed class Settings
    {
        /// <summary>
        /// F8 by default, and deliberately not the left arrow.
        ///
        /// TextDispatch uses the left arrow, and if both plugins are installed then two boxes on one
        /// key would both open and both read the same keys. F8 is free on this install: F4 is RAGE
        /// Plugin Hook's console, F7 is ScriptHookVDotNet's, and DriverJobs uses Shift+J.
        /// </summary>
        public string OpenKey = "F8";

        /// <summary>top-left, top-right, bottom-left, bottom-right. Top-right, as TextDispatch does.</summary>
        public string ChatPosition = "top-right";

        public float ChatMargin = 16f;
        public float FontScale = 0.34f;
        public int Lines = 12;

        /// <summary>How long the last output stays on screen after the box closes.</summary>
        public int TranscriptSeconds = 25;

        /// <summary>Hide what is typed from the other plugins, as TextDispatch does.</summary>
        public bool BlockOtherModsKeys = true;

        /// <summary>And release a hidden key in the hardware state too, for plugins that read it directly.</summary>
        public bool HideHardwareKeys = true;

        private static readonly string[] KnownKeys =
        {
            "openkey", "chatposition", "chatmargin", "fontscale", "lines", "transcriptseconds",
            "blockothermodskeys", "hidehardwarekeys"
        };

        /// <summary>The folder the script is in - which is the game's scripts folder.</summary>
        public static string Folder()
        {
            try
            {
                var beside = Path.GetDirectoryName(typeof(Settings).Assembly.Location);
                if (!string.IsNullOrEmpty(beside) && Directory.Exists(beside)) return beside;
            }
            catch { }
            return Environment.CurrentDirectory;
        }

        public static string IniPath() { return Path.Combine(Folder(), "TextJobs.ini"); }

        public static Settings Load()
        {
            var settings = new Settings();
            try
            {
                var path = IniPath();
                if (!File.Exists(path)) { Write(path, settings); return settings; }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                    var split = line.IndexOf('=');
                    if (split <= 0) continue;

                    var key = line.Substring(0, split).Trim();
                    var value = line.Substring(split + 1).Trim();
                    seen.Add(key);
                    if (value.Length == 0) continue;

                    switch (key.ToLowerInvariant())
                    {
                        case "openkey": settings.OpenKey = value; break;
                        case "chatposition": settings.ChatPosition = value; break;
                        case "chatmargin": settings.ChatMargin = AsFloat(value, settings.ChatMargin); break;
                        case "fontscale": settings.FontScale = AsFloat(value, settings.FontScale); break;
                        case "lines": settings.Lines = AsInt(value, settings.Lines); break;
                        case "transcriptseconds": settings.TranscriptSeconds = AsInt(value, settings.TranscriptSeconds); break;
                        case "blockothermodskeys": settings.BlockOtherModsKeys = AsBool(value, settings.BlockOtherModsKeys); break;
                        case "hidehardwarekeys": settings.HideHardwareKeys = AsBool(value, settings.HideHardwareKeys); break;
                    }
                }

                // A file from an older version is missing whatever was added since, and would answer
                // silently with defaults. Bring it up to date once, keeping what is already there.
                foreach (var known in KnownKeys)
                {
                    if (seen.Contains(known)) continue;
                    Write(path, settings);
                    Log.Line("settings: " + path + " predates some current keys; rewritten with the full set");
                    break;
                }
            }
            catch (Exception ex) { Log.Error("settings", ex); }

            return settings;
        }

        public void Save() { Write(IniPath(), this); }

        private static void Write(string path, Settings settings)
        {
            try
            {
                File.WriteAllLines(path, new[]
                {
                    "; TextJobs settings. Delete this file to get the defaults back.",
                    ";",
                    "; Which key opens the box. F8 by default: F4 is RAGE Plugin Hook's console, F7 is",
                    "; ScriptHookVDotNet's, DriverJobs itself uses Shift+J, and TextDispatch uses the left",
                    "; arrow - two chat boxes on one key would both open and both read the same keys.",
                    "OpenKey=" + settings.OpenKey,
                    "",
                    "; Where the box sits: top-left, top-right, bottom-left, bottom-right.",
                    "; Top-right, the same corner TextDispatch uses, because the other LSPDFR plugins draw",
                    "; on the left.",
                    "ChatPosition=" + settings.ChatPosition,
                    "; Distance from the screen edge, in pixels.",
                    "ChatMargin=" + settings.ChatMargin.ToString(CultureInfo.InvariantCulture),
                    "; How big the text is, and how many lines of the transcript are shown.",
                    "FontScale=" + settings.FontScale.ToString(CultureInfo.InvariantCulture),
                    "Lines=" + settings.Lines.ToString(CultureInfo.InvariantCulture),
                    "",
                    "; How long the last output stays on screen after the box closes, in seconds.",
                    "TranscriptSeconds=" + settings.TranscriptSeconds.ToString(CultureInfo.InvariantCulture),
                    "",
                    "; Whether what you type is hidden from the other plugins while the box is open.",
                    "; Without it, typing a job name opens whatever menu is watching for those letters.",
                    "; /hide off changes it while playing.",
                    "BlockOtherModsKeys=" + (settings.BlockOtherModsKeys ? "1" : "0"),
                    "; Whether a hidden key is also released in the hardware state, which is what stops a",
                    "; plugin that reads the keyboard directly instead of the game's messages.",
                    "HideHardwareKeys=" + (settings.HideHardwareKeys ? "1" : "0")
                });
            }
            catch (Exception ex) { Log.Error("write settings", ex); }
        }

        private static int AsInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static float AsFloat(string value, float fallback)
        {
            float parsed;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static bool AsBool(string value, bool fallback)
        {
            var text = (value ?? "").Trim();
            if (text.Length == 0) return fallback;
            if (text == "1" || text.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (text == "0" || text.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }
    }
}
