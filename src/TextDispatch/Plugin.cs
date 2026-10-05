using System;
using Rage;
using TextDispatch.Bridges;
using TextDispatch.Chat;
using TextDispatch.Commands;
using TextDispatch.Dialogue;
using TextDispatch.Lspdfr;
using TextDispatch.Records;

namespace TextDispatch
{
    /// <summary>
    /// The engine, and the host-facing lifetime.
    ///
    /// There is deliberately no RAGE Plugin Hook plugin attribute here. LSPDFR does not use it for
    /// the plugins it loads: it instantiates a class named "Main" deriving from its own Plugin base
    /// (see Main.cs). Being loaded by LSPDFR is not a style choice - it is the only way to run
    /// inside LSPDFR's AppDomain, and a plugin RPH loads gets its own AppDomain and cannot see
    /// LSPDFR at all.
    /// </summary>
    public static class Plugin
    {
        private static ChatBox _chat;
        private static TextInput _input;
        private static LspdfrApi _api;
        private static DispatchService _dispatch;
        private static DialogueService _dialogue;
        private static Settings _settings;
        private static RecordsLedger _records;
        private static FrameworkBridge _bridge;
        private static CommandRouter _router;
        private static PluginInventory _inventory;
        private static bool _rendering;
        private static bool _renderErrorLogged;
        private static bool _controlsFailed;

        // Diagnostics for the one thing that cannot be tested outside the game: is the box drawn?
        private static int _renderCalls;
        private static bool _renderReported;
        private static int _startedAt;
        private static string _lastRenderError;
        private static bool _inventoryReported;

        private static bool _running;
        private static GameFiber _fiber;

        internal static int RenderCalls { get { return _renderCalls; } }
        internal static string LastRenderError { get { return _lastRenderError; } }

        public static ChatBox Chat { get { return _chat; } }

        internal static LspdfrApi Api { get { return _api; } }
        internal static CommandRouter Router { get { return _router; } }
        internal static DialogueService Dialogue { get { return _dialogue; } }
        internal static TextInput Input { get { return _input; } }
        internal static DispatchService Dispatch { get { return _dispatch; } }
        internal static Settings Settings { get { return _settings; } }
        internal static PluginInventory Inventory { get { return _inventory; } }

        /// <summary>What other plugins can do - K9 units, spike strips, the checks they own.</summary>
        internal static FrameworkBridge Bridge { get { return _bridge; } }

        /// <summary>What the box opens with, for saying it back: 'F6', or 'T or F6'.</summary>
        internal static string OpenKeyDescription
        {
            get
            {
                if (_input != null) return _input.OpenKeyDescription;
                return _settings == null ? "" : _settings.OpenKey;
            }
        }

        /// <summary>
        /// Change the key, or keys, that open the box, and remember it in the ini.
        ///
        /// In game rather than only in the ini, because the key that clashes is discovered while
        /// playing - by pressing it and watching something else come up. Returns null on success, or
        /// a sentence saying what was wrong.
        /// </summary>
        internal static string ChangeOpenKey(string spec, out string description)
        {
            description = null;
            if (string.IsNullOrWhiteSpace(spec)) return "no key was given";
            if (_input == null || _settings == null) return "the box is not running";

            if (!_input.SetOpenKey(spec))
                return "'" + spec.Trim() + "' is not a key name - try F6, T, Home, OemQuestion";

            _settings.OpenKey = spec.Trim();
            _settings.Save();
            description = _input.OpenKeyDescription;
            return null;
        }

        /// <summary>
        /// Called by LSPDFR. This must return - LSPDFR is waiting on it - so the tick loop is handed
        /// to its own fiber rather than run here. A loop in this method hangs LSPDFR's startup.
        /// </summary>
        public static void Start()
        {
            if (_running) return;

            try
            {
                // Without this the box would only hear the keyboard while the game has focus, which
                // in borderless windowed mode is not where a player's attention always is.
                Game.AlwaysReceiveKeyEvents = true;

                _settings = Settings.Load();
                _settings.Resolve();

                _chat = new ChatBox();
                _api = new LspdfrApi();

                // The other frameworks are found by reflection, the same way LSPDFR is, so nothing is
                // referenced at build time and a missing one costs nothing but the commands it owned.
                // Ultimate Backup sorts after us in the folder, so discovery is repeated on demand.
                _bridge = new FrameworkBridge();
                _bridge.Discover();
                // Fixed seed: the same town every session, so somebody the terminal flags as wanted is
                // still that person tomorrow. The alternative - a fresh population each launch - makes
                // the records meaningless.
                //
                // Built before the services that read it, on purpose: a plate on the radio is answered
                // from the same records the terminal shows, and the conversation prompt describes the
                // same person the terminal does.
                _records = RecordsLedger.Populate(20261004);
                _dispatch = new DispatchService(_chat, _api, _settings, _records);
                _dialogue = new DialogueService(_chat, _api, _settings, _records);
                _router = new CommandRouter(_chat, _api, _dispatch, _dialogue, _settings, _records);
                _input = new TextInput(_chat, _router.Handle);

                if (!_input.SetOpenKey(_settings.OpenKey))
                    Log.Line("OpenKey '" + _settings.OpenKey + "' is not a key name; sticking with T");

                ChatCorner corner;
                if (ChatCorners.TryParse(_settings.ChatPosition, out corner)) _chat.Position = corner;
                else Log.Line("ChatPosition '" + _settings.ChatPosition + "' is not a corner; using top-right");
                _chat.Margin = _settings.ChatMargin;

                // RawFrameRender, not FrameRender. FrameRender is called per *game tick* - about 23
                // times a second - and its draw calls get queued, which makes a HUD flicker against a
                // 60fps frame rate. RawFrameRender is called once per frame, which is what a chat box
                // wants. It does not allow native calls; this only uses managed drawing.
                Game.RawFrameRender += OnFrameRender;
                Game.AddConsoleCommands(new Type[] { typeof(ConsoleCommands) });

                // A reload - ReloadAllPlugins in the F4 console, or LSPDFR restarting us - comes
                // back through here, and both of the deferred checks below have to run again on the
                // new world rather than be skipped because they already happened once.
                _startedAt = Environment.TickCount;
                _renderReported = false;
                _inventoryReported = false;

                Log.Line("starting; " + _api.Describe() + "; npc speech mode=" + _settings.AiMode +
                         "; appdomain=" + AppDomain.CurrentDomain.FriendlyName);
                Log.Line("settings: " + Settings.IniPath());
                Log.Line("log: " + Log.Path);
                ReportDisplay();

                // Two lines at startup, not five. A box that opens with a wall of text is one the
                // player scrolls past rather than reads, and everything else belongs in /help where
                // it can be asked for.
                _chat.Notice("TextDispatch ready. Press " + _input.OpenKeyDescription +
                             " to chat - type to speak, /r for the radio, /help for the rest.");
                _chat.Dispatch("Dispatch online. " + _dispatch.Unit +
                               ", you are 10-8. I will call you when something comes in.");

                // Pull the model into memory now rather than on the player's first sentence.
                Ai.LocalModel.WarmUp(_settings);

                // An RPH notification as well as a chat line.
                //
                // The question a player actually has is "did it load?", and until now the only answer
                // was the chat box itself - which is exactly the thing that is missing when it has not
                // loaded. LSPDFR does not load these plugins until about half a minute after the
                // player switch, so pressing T too early looks identical to a broken install.
                try { Game.DisplayNotification("TextDispatch loaded. Press T to chat."); }
                catch { }

                _running = true;
                _fiber = GameFiber.StartNew(Loop, "TextDispatch");
            }
            catch (Exception ex) { Log.Error("startup", ex); }
        }

        /// <summary>
        /// Tear the engine down. Deliberately **not** called from Main.Finally(): LSPDFR calls that
        /// when the player goes off duty, and stopping there left the chat box dead for the rest of
        /// the session. It is kept for a real unload, if LSPDFR ever grows one.
        /// </summary>
        public static void Stop()
        {
            _running = false;

            try { Game.RawFrameRender -= OnFrameRender; }
            catch { }

            Log.Line("stopping");
        }

        private static void Loop()
        {
            while (_running)
            {
                try
                {
                    _input.Update();

                    // While the box is open the player is typing, not driving. Freezing the game's
                    // controls is what makes typing "10-97" not also steer the car.
                    if (_chat.IsOpen) DisableGameControls();

                    _dispatch.Update();
                    _dialogue.Update();

                    // Five seconds in, say plainly whether anything is being drawn. This is the single
                    // question that cannot be answered from outside the game, and it should not be
                    // left to guesswork.
                    if (!_renderReported && Environment.TickCount - _startedAt > 5000)
                    {
                        _renderReported = true;
                        Log.Line("render check: the render callback has fired " + _renderCalls +
                                 " time(s) in 5s" +
                                 (_renderCalls == 0
                                    ? "  -- THE CHAT BOX IS NOT BEING DRAWN. The plugin loaded, so this is the render path, not the load."
                                    : "  -- the box is being drawn."));
                    }

                    // And a few seconds after that, the other question nobody can answer from
                    // outside the game: did everything in the plugins folder actually load?
                    //
                    // LSPDFR constructs every plugin in one pass around the moment we start, so by
                    // now the answer is final. Checked once and then dropped - a folder scan on
                    // every tick would be absurd for something that only changes between sessions.
                    if (!_inventoryReported && Environment.TickCount - _startedAt > 8000)
                    {
                        _inventoryReported = true;
                        ReportInventory();
                    }
                }
                catch (Exception ex) { Log.Error("tick", ex); }

                // Removing this would hang the game: RPH runs plugins on fibers.
                GameFiber.Yield();
            }
        }

        /// <summary>
        /// Say what loaded, and what did not. A plugin folder that quietly does nothing is the
        /// single most common way an LSPDFR install is broken, and the reason is never visible from
        /// inside the game - so it gets said in the box, and written down in full in the log.
        /// </summary>
        private static void ReportInventory()
        {
            _inventory = PluginInventory.Scan(_api);
            _inventory.WriteToLog();

            // By now every plugin in the folder has been constructed, so this is the first moment the
            // framework bridge can be sure it found all of them.
            if (_bridge != null) _bridge.Ensure();

            // The headline, and only the warnings that mean something is wrong.
            //
            // "Optional integrations not installed" is the common case - a feature switched off,
            // not a fault - and a long line about it every session is how a box teaches its reader
            // to look past it. /plugins still lists it and the log has it in full.
            _chat.Notice(_inventory.Headline());
            foreach (var warning in _inventory.Warnings())
            {
                if (warning.StartsWith("Optional integrations", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Line("inventory (kept out of the box - it is a feature switched off, not a fault): " + warning);
                    continue;
                }

                _chat.Error(warning);
            }
        }

        private static void DisableGameControls()
        {
            // Per-frame: it lapses by itself on the next frame, so there is nothing to undo when the
            // box closes.
            if (_controlsFailed) return;

            try { Rage.Native.NativeFunction.Natives.DisableAllControlActions(0); }
            catch (Exception ex)
            {
                // Called every frame, so a failure is reported once and then left alone - otherwise
                // one wrong native name would fill the log in seconds.
                _controlsFailed = true;
                Log.Error("disable controls (typing will also steer; everything else still works)", ex);
            }
        }

        private static void OnFrameRender(object sender, GraphicsEventArgs e)
        {
            if (_rendering) return;
            _rendering = true;
            _renderCalls++;

            try
            {
                _chat.Render(e.Graphics);
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                _lastRenderError = inner.GetType().Name + ": " + inner.Message;

                if (!_renderErrorLogged)
                {
                    _renderErrorLogged = true;
                    Log.Error("render", ex);
                }
            }
            finally { _rendering = false; }
        }

        /// <summary>
        /// Write down what the game reports about its display and fonts, because those are the two
        /// things most likely to make a working plugin look like a broken one.
        /// </summary>
        private static void ReportDisplay()
        {
            try
            {
                var resolution = Game.Resolution;
                Log.Line("display: game reports " + resolution.Width.ToString("0") + "x" + resolution.Height.ToString("0") +
                         (resolution.Width > 0f && resolution.Height > 0f
                            ? ""
                            : "  (unusable - layout falls back to 1920x1080)"));
            }
            catch (Exception ex) { Log.Error("read resolution", ex); }

            try
            {
                var size = Graphics.MeasureText("TextDispatch", _chat.FontName, _chat.FontSize);
                Log.Line("display: font '" + _chat.FontName + "' measures \"TextDispatch\" as " +
                         size.Width.ToString("0.0") + "x" + size.Height.ToString("0.0") +
                         (size.Width <= 0f ? "  -- THE FONT PROBABLY DOES NOT EXIST; try /font Consolas" : ""));
            }
            catch (Exception ex) { Log.Error("measure font", ex); }
        }
    }
}
