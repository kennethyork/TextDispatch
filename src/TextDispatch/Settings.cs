using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace TextDispatch
{
    /// <summary>
    /// Settings, read from TextDispatch.ini next to the plugin. An ini rather than a compiled-in
    /// constant because the things people will want to change - which model drives the NPCs and the
    /// dispatcher, and how chat ranges feel - are not things to require a rebuild for.
    /// </summary>
    internal sealed class Settings
    {
        // ---------------------------------------------------------------- AI

        /// <summary>auto | llm | scripted. "auto" uses a model if one answers, and the script if not.</summary>
        public string AiMode = "auto";

        /// <summary>ollama | lmstudio | anything else is treated as OpenAI-compatible.</summary>
        public string AiProvider = "ollama";

        /// <summary>Blank means "whatever the provider defaults to".</summary>
        public string AiEndpoint = "";

        /// <summary>Blank means "the first model the server reports".</summary>
        public string AiModel = "";

        /// <summary>
        /// How long a reply may take before the scripted line is used. Raised with the longer, more
        /// conversational answers: a two-sentence reply on a CPU takes noticeably longer than a clipped
        /// one, and falling back would undo the point of asking for a conversation.
        /// </summary>
        public int AiTimeoutMs = 15000;

        /// <summary>Room for one or two sentences rather than one clipped line.</summary>
        public int AiMaxTokens = 160;

        public float AiTemperature = 0.9f;

        /// <summary>
        /// How much of the exchange is handed to the model. Six lines is about two exchanges, which is
        /// not enough for somebody to know what they said a moment ago - the main reason conversations
        /// felt like unrelated one-liners.
        /// </summary>
        public int AiHistoryLines = 20;

        /// <summary>Set once, at startup, from AiMode.</summary>
        private bool _useModel;
        public bool UseModel { get { return _useModel; } }

        /// <summary>Filled in by the model layer when the server tells us what it has.</summary>
        public string ResolvedModel;

        /// <summary>
        /// Decide once whether a model is actually in play. Doing it here means the rest of the
        /// plugin never has to ask, and a machine with no model running behaves exactly as it did
        /// before any of this existed.
        /// </summary>
        public void Resolve()
        {
            if ("llm".Equals(AiMode, StringComparison.OrdinalIgnoreCase)) { _useModel = true; return; }
            if ("scripted".Equals(AiMode, StringComparison.OrdinalIgnoreCase)) { _useModel = false; return; }

            _useModel = Ai.LocalModel.Available(this);
            Log.Line("ai mode auto: " + (_useModel
                ? "a model answered at " + Ai.LocalModel.Endpoint(this)
                : "no model at " + Ai.LocalModel.Endpoint(this) + " - using the script"));
        }

        public void ForceMode(string mode)
        {
            AiMode = mode;
            ResolvedModel = null;
            Resolve();
        }

        // ---------------------------------------------------------------- speech

        /// <summary>
        /// Which key opens the chat box. Configurable because T is a popular key - with a few mods
        /// installed it is entirely possible something else already owns it, and a chat box you
        /// cannot open is not much of a chat box.
        /// </summary>
        public string OpenKey = "T";

        /// <summary>
        /// Which corner the box sits in. Top-right by default: other LSPDFR plugins draw on the
        /// left, and two panels in the same corner make each other unreadable.
        /// </summary>
        public string ChatPosition = "top-right";

        /// <summary>Distance from the screen edge, in pixels.</summary>
        public float ChatMargin = 16f;

        public int TypingMs = 900;                  // NPC "typing" pause
        public int DispatchMs = 500;                // radio answers come back faster
        public float SayRange = 15f;
        public float WhisperRange = 3f;
        public float ShoutRange = 35f;

        // ---------------------------------------------------------------- load

        /// <summary>
        /// Write the settings back. Used when a runtime command changes something worth keeping -
        /// a box position the player had to fiddle with is exactly that.
        /// </summary>
        public void Save()
        {
            Write(IniPath(), this);
        }

        /// <summary>
        /// Every setting this version knows about. Used to notice a settings file written by an older
        /// version, which is how a documented setting quietly came to be missing from a file that was
        /// supposed to contain it.
        /// </summary>
        private static readonly string[] KnownKeys =
        {
            "aimode", "aiprovider", "aiendpoint", "aimodel", "aitimeoutms", "aimaxtokens",
            "aitemperature", "aihistorylines", "openkey", "chatposition", "chatmargin", "typingms",
            "dispatchms", "sayrange", "whisperrange", "shoutrange"
        };

        public static Settings Load()
        {
            var settings = new Settings();
            try
            {
                var path = IniPath();

                // Version 1.0.3 and earlier wrote this to the game's root folder, because the
                // assembly reported no location. Move it rather than abandoning the player's
                // settings and starting a second copy.
                var stray = Path.Combine(Environment.CurrentDirectory, "TextDispatch.ini");
                if (!File.Exists(path) && File.Exists(stray) && !string.Equals(stray, path, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Move(stray, path);
                        Log.Line("settings: moved TextDispatch.ini into " + PluginFolder());
                    }
                    catch { }
                }

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

                    // Recorded before the empty check: blank is a legitimate value for AiEndpoint and
                    // AiModel, meaning "ask the server", and treating those as absent would make the
                    // file look incomplete on every single load.
                    seen.Add(key);

                    if (value.Length == 0) continue;

                    switch (key.ToLowerInvariant())
                    {
                        case "aimode": settings.AiMode = value; break;
                        case "aiprovider": settings.AiProvider = value; break;
                        case "aiendpoint": settings.AiEndpoint = value; break;
                        case "aimodel": settings.AiModel = value; break;
                        case "aitimeoutms": settings.AiTimeoutMs = AsInt(value, settings.AiTimeoutMs); break;
                        case "aimaxtokens": settings.AiMaxTokens = AsInt(value, settings.AiMaxTokens); break;
                        case "aitemperature": settings.AiTemperature = AsFloat(value, settings.AiTemperature); break;
                        case "aihistorylines": settings.AiHistoryLines = AsInt(value, settings.AiHistoryLines); break;
                        case "openkey": settings.OpenKey = value; break;
                        case "chatposition": settings.ChatPosition = value; break;
                        case "chatmargin": settings.ChatMargin = AsFloat(value, settings.ChatMargin); break;
                        case "typingms": settings.TypingMs = AsInt(value, settings.TypingMs); break;
                        case "dispatchms": settings.DispatchMs = AsInt(value, settings.DispatchMs); break;
                        case "sayrange": settings.SayRange = AsFloat(value, settings.SayRange); break;
                        case "whisperrange": settings.WhisperRange = AsFloat(value, settings.WhisperRange); break;
                        case "shoutrange": settings.ShoutRange = AsFloat(value, settings.ShoutRange); break;
                    }
                }

                // A file written by an older version is missing whatever was added since, and answers
                // silently with defaults while offering no key to find or change. Bring it up to date
                // once, keeping the values that are already there.
                var incomplete = false;
                foreach (var known in KnownKeys)
                {
                    if (seen.Contains(known)) continue;
                    incomplete = true;
                    break;
                }

                if (incomplete)
                {
                    Write(path, settings);
                    Log.Line("settings: " + path + " predates some current keys; rewritten with the full set");
                }
            }
            catch (Exception ex) { Log.Error("settings", ex); }

            return settings;
        }

        private static void Write(string path, Settings settings)
        {
            try
            {
                File.WriteAllLines(path, new[]
                {
                    "; TextDispatch settings. Delete this file to get the defaults back.",
                    ";",
                    "; AiMode: auto     = use a model if one is running, the built-in script if not",
                    ";         llm      = always use a model",
                    ";         scripted = never use a model",
                    "AiMode=" + settings.AiMode,
                    ";",
                    "; AiProvider: ollama (default) or lmstudio. Anything else is treated as an",
                    "; OpenAI-compatible chat server.",
                    "AiProvider=" + settings.AiProvider,
                    ";",
                    "; AiEndpoint: leave blank for the provider default -",
                    ";   ollama    http://localhost:11434/api/chat",
                    ";   lmstudio  http://localhost:1234/v1/chat/completions",
                    "AiEndpoint=" + settings.AiEndpoint,
                    ";",
                    "; AiModel: leave blank to use the first model the server reports.",
                    "; For Ollama that is whatever you have pulled, e.g. llama3.2, qwen2.5, mistral.",
                    "AiModel=" + settings.AiModel,
                    "AiTimeoutMs=" + settings.AiTimeoutMs.ToString(CultureInfo.InvariantCulture),
                    "AiMaxTokens=" + settings.AiMaxTokens.ToString(CultureInfo.InvariantCulture),
                    "AiTemperature=" + settings.AiTemperature.ToString(CultureInfo.InvariantCulture),
                    "; How much of the conversation to hand the model. Longer means it remembers more,",
                    "; and costs a little more time per reply.",
                    "AiHistoryLines=" + settings.AiHistoryLines.ToString(CultureInfo.InvariantCulture),
                    "",
                    "; Which key opens the chat box. Any key name works: T, F6, OemQuestion, Home.",
                    "; Change it if another mod already uses T.",
                    "OpenKey=" + settings.OpenKey,
                    "",
                    "; Which corner the chat box sits in: top-left, top-right, bottom-left, bottom-right.",
                    "; Top-right by default, because other LSPDFR plugins draw on the left.",
                    "ChatPosition=" + settings.ChatPosition,
                    "; Distance from the screen edge, in pixels.",
                    "ChatMargin=" + settings.ChatMargin.ToString(CultureInfo.InvariantCulture),
                    "",
                    "; How long an NPC appears to 'type' before answering, in milliseconds.",
                    "; The real delay scales with the length of the reply, between half and double this.",
                    "TypingMs=" + settings.TypingMs,
                    "; Radio answers come back faster than people type.",
                    "DispatchMs=" + settings.DispatchMs,
                    "",
                    "; Chat ranges in metres.",
                    "SayRange=" + settings.SayRange.ToString(CultureInfo.InvariantCulture),
                    "WhisperRange=" + settings.WhisperRange.ToString(CultureInfo.InvariantCulture),
                    "ShoutRange=" + settings.ShoutRange.ToString(CultureInfo.InvariantCulture)
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

        /// <summary>
        /// Where the plugin's settings belong: beside the DLL, in Plugins\LSPDFR.
        ///
        /// Assembly.Location cannot be relied on here. LSPDFR loads its plugins from memory, and an
        /// assembly loaded that way reports an empty Location - which silently sent the ini to the
        /// game's root folder instead. So the location is used when it exists, and otherwise the
        /// plugin's folder is found the way LSPDFR itself refers to it.
        /// </summary>
        public static string PluginFolder()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                {
                    var directory = Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) return directory;
                }
            }
            catch { }

            // Two anchors, because the working directory is normally the game folder but is not
            // guaranteed to be.
            foreach (var root in new[] { AppDomain.CurrentDomain.BaseDirectory, Environment.CurrentDirectory })
            {
                if (string.IsNullOrEmpty(root)) continue;
                try
                {
                    var besideTheGame = Path.Combine(root, "Plugins", "LSPDFR");
                    if (Directory.Exists(besideTheGame)) return besideTheGame;
                }
                catch { }
            }

            return Environment.CurrentDirectory;
        }

        /// <summary>The settings file, wherever it has ended up.</summary>
        public static string IniPath()
        {
            return Path.Combine(PluginFolder(), "TextDispatch.ini");
        }
    }
}
