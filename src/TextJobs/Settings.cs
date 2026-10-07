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
        /// "auto", which means the left arrow - the key the player already uses for the police box -
        /// unless the police box is loaded in this session, in which case the left arrow is spoken
        /// for and this one uses F9.
        ///
        /// F9 is not an arbitrary second choice: the rest of the function row is taken. F4 is RAGE
        /// Plugin Hook's console, F7 is ScriptHookVDotNet's, F5 and F6 are Traffic Policer's, F8 is
        /// ALPRLite's, F10 is StopThePed's search, F11 and F12 are LSPDFR's, and DriverJobs uses
        /// Shift+J.
        ///
        /// This is a key rather than a preference to be read out of the ini, because the answer
        /// depends on what else is loaded - and off LSPDFR, which is when this box is the only one,
        /// the left arrow is the obvious key and there is nothing to clash with. Set OpenKey to a
        /// key name and that wins; the ini says so.
        /// </summary>
        public string OpenKey = "auto";

        /// <summary>
        /// The file TextDispatch refreshes about twice a second while its box is loaded, which is how
        /// this plugin knows whether the left arrow is spoken for.
        ///
        /// The name is the interface between the two plugins, and it cannot be a reference between
        /// them: TextDispatch is an LSPDFR plugin in LSPDFR's own AppDomain, and a ScriptHookVDotNet
        /// script cannot see it. tools\verify-bridge.ps1 reads both assemblies and checks that these
        /// two constants still match, because a rename here would fail silently - as a wrong key.
        /// </summary>
        public const string PoliceAliveFile = "TextDispatch-alive";

        /// <summary>top-left, top-right, bottom-left, bottom-right. Top-right, as TextDispatch does.</summary>
        public string ChatPosition = "top-right";

        public float ChatMargin = 16f;

        /// <summary>
        /// The text size, in pixels at 1080p - the same units and the same default as TextDispatch's
        /// FontSize, because the two boxes are meant to be the same box.
        /// </summary>
        public float FontSize = 15f;

        /// <summary>Everything scales from this, as TextDispatch's own UiScale does.</summary>
        public float UiScale = 1f;

        /// <summary>Ten, the same as TextDispatch's box, so both show the same screenful. It is rows,
        /// not entries: a line long enough to wrap inside the panel takes two of them.</summary>
        public int Lines = 10;

        /// <summary>
        /// Seconds the transcript stays after the box closes. Zero means it stays, which is what the
        /// police box does and therefore what this one does now.
        /// </summary>
        public int TranscriptSeconds = 0;

        /// <summary>Hide what is typed from the other plugins, as TextDispatch does.</summary>
        public bool BlockOtherModsKeys = true;

        /// <summary>And release a hidden key in the hardware state too, for plugins that read it directly.</summary>
        public bool HideHardwareKeys = true;

        private static readonly string[] KnownKeys =
        {
            "openkey", "chatposition", "chatmargin", "fontsize", "uiscale", "lines", "transcriptseconds",
            "blockothermodskeys", "hidehardwarekeys"
        };

        /// <summary>
        /// The folder the script is in, which is the game's scripts folder.
        ///
        /// Assembly.Location cannot be relied on: ScriptHookVDotNet loads its scripts from memory, and
        /// an assembly loaded that way reports an empty location - the same trap TextDispatch hit.
        ///
        /// What it falls back to matters more than it looks. A session on 6 October loaded this script,
        /// drew its box, and wrote no log at all: the path it settled on was writable by nobody, the
        /// write failed, and the failure was swallowed - which is the worst way for a diagnostic to
        /// fail, because it looks exactly like a script that never ran. So the game's own folder is
        /// found from the running process, and the candidates below are tried in order, each one having
        /// to exist before it is accepted.
        ///
        /// <see cref="Log"/> then goes further and checks that a file can actually be written before
        /// it commits to a path.
        /// </summary>
        public static string Folder()
        {
            try
            {
                var beside = Path.GetDirectoryName(typeof(Settings).Assembly.Location);
                if (!string.IsNullOrEmpty(beside) && Directory.Exists(beside)) return beside;
            }
            catch { }

            // Where the game is, from the process that is running it. This is the one path that cannot
            // be wrong, and it is what a native host - which is what ScriptHookVDotNet runs inside -
            // does not always provide as AppDomain.BaseDirectory.
            var game = GameFolder();
            if (game != null)
            {
                var scripts = Path.Combine(game, "scripts");
                if (Directory.Exists(scripts)) return scripts;
                return game;
            }

            foreach (var root in new[] { AppDomain.CurrentDomain.BaseDirectory, Environment.CurrentDirectory })
            {
                if (string.IsNullOrEmpty(root)) continue;
                try
                {
                    var scripts = Path.Combine(root, "scripts");
                    if (Directory.Exists(scripts)) return scripts;
                }
                catch { }
            }

            return Environment.CurrentDirectory;
        }

        /// <summary>
        /// The folder the game is running from, worked out from the process.
        ///
        /// This is the answer that does not depend on how the host happened to set the AppDomain up:
        /// the process that is running this script is GTA5.exe, and its folder is the game folder.
        /// Null when the process cannot be asked, and the caller then falls back as it always did.
        /// </summary>
        public static string GameFolder()
        {
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                if (exe == null || string.IsNullOrEmpty(exe.FileName)) return null;

                var folder = Path.GetDirectoryName(exe.FileName);
                return !string.IsNullOrEmpty(folder) && Directory.Exists(folder) ? folder : null;
            }
            catch { return null; }
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
                        case "fontsize": settings.FontSize = AsFloat(value, settings.FontSize); break;
                        case "uiscale": settings.UiScale = AsFloat(value, settings.UiScale); break;

                        // The first version of this file sized the text as a ScriptHookVDotNet scale,
                        // 0.34 by default. A file from then still works: 0.34 was 15px, so the old
                        // number is converted rather than ignored, and the ini is rewritten with the
                        // pixel names the next time it is opened.
                        case "fontscale":
                            var legacy = AsFloat(value, 0f);
                            if (legacy > 0f) settings.FontSize = 15f * (legacy / 0.34f);
                            break;

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

        /// <summary>
        /// Save, and if the first place will not take it, the game's own folder - the same idea as the
        /// log having two channels. The ini was never written on this machine in a whole session, and a
        /// settings file that cannot be written is a settings file that cannot be changed.
        /// </summary>
        public void Save()
        {
            var path = IniPath();
            if (Write(path, this)) return;

            var game = GameFolder();
            if (game == null) return;

            var fallback = Path.Combine(game, "TextJobs.ini");
            if (Write(fallback, this)) Log.Line("settings: could not write " + path + " - the ini is at " + fallback);
        }

        private static bool Write(string path, Settings settings)
        {
            try
            {
                File.WriteAllLines(path, new[]
                {
                    "; TextJobs settings. Delete this file to get the defaults back.",
                    ";",
                    "; Which key opens the box: auto, or any key name (Left, F9, Numpad0, OemQuestion).",
                    ";",
                    "; auto means the LEFT ARROW - the key the police box uses - unless TextDispatch's box is",
                    "; loaded in this session, in which case the left arrow is spoken for and this box",
                    "; uses F9. Off LSPDFR, when this is the only box, the left arrow is yours with",
                    "; nothing to clash with.",
                    ";",
                    "; F9 is the fallback rather than a preference: F4 is RAGE Plugin Hook's console, F7 is",
                    "; ScriptHookVDotNet's, F5 and F6 are Traffic Policer's, F8 is ALPRLite's, F10 is",
                    "; StopThePed's search, F11 and F12 are LSPDFR's, and DriverJobs uses Shift+J.",
                    ";",
                    "; Name a key here and that wins over auto.",
                    "OpenKey=" + settings.OpenKey,
                    "",
                    "; Where the box sits: top-left, top-right, bottom-left, bottom-right.",
                    "; Top-right, the same corner TextDispatch uses, because the other LSPDFR plugins draw",
                    "; on the left.",
                    "ChatPosition=" + settings.ChatPosition,
                    "; Distance from the screen edge, in pixels.",
                    "ChatMargin=" + settings.ChatMargin.ToString(CultureInfo.InvariantCulture),
                    "; How big the text is, and how many rows of the transcript are shown. FontSize is",
                    "; in pixels at 1080p, the same units TextDispatch.ini uses. A row is not an entry:",
                    "; a line too long for the panel wraps and takes two or three of them.",
                    "FontSize=" + settings.FontSize.ToString(CultureInfo.InvariantCulture),
                    "UiScale=" + settings.UiScale.ToString(CultureInfo.InvariantCulture),
                    "Lines=" + settings.Lines.ToString(CultureInfo.InvariantCulture),
                    "",
                    "; How long the transcript stays after the box closes, in seconds. 0 means it stays,",
                    "; which is what the police box does.",
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
                return true;
            }
            catch (Exception ex) { Log.Error("write settings to " + path, ex); return false; }
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
