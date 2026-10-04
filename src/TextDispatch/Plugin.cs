using System;
using Rage;
using Rage.Attributes;
using TextDispatch.Chat;
using TextDispatch.Commands;
using TextDispatch.Dialogue;
using TextDispatch.Lspdfr;

// RPH finds plugins by this attribute; EntryPoint is named explicitly so there is no chance of it
// picking some other static Main.
[assembly: Plugin("TextDispatch",
    Description = "LSPDFR in text: callouts, dispatch and interactions through an in-game chat box.",
    EntryPoint = "TextDispatch.Plugin.Main")]

namespace TextDispatch
{
    /// <summary>
    /// The plugin entry point, and the fiber loop.
    ///
    /// Everything that touches the game happens here, on the fiber: reading the keyboard, polling
    /// LSPDFR, and freezing the player's controls while they type. Drawing happens on the render
    /// callback instead, which is a different thread - hence the lock inside ChatBox.
    /// </summary>
    public static class Plugin
    {
        private static ChatBox _chat;
        private static TextInput _input;
        private static LspdfrApi _api;
        private static DispatchService _dispatch;
        private static DialogueService _dialogue;
        private static Settings _settings;
        private static CommandRouter _router;
        private static bool _rendering;
        private static bool _renderErrorLogged;
        private static bool _controlsFailed;

        public static ChatBox Chat { get { return _chat; } }

        internal static LspdfrApi Api { get { return _api; } }
        internal static CommandRouter Router { get { return _router; } }
        internal static DialogueService Dialogue { get { return _dialogue; } }
        internal static TextInput Input { get { return _input; } }
        internal static DispatchService Dispatch { get { return _dispatch; } }
        internal static Settings Settings { get { return _settings; } }

        public static void Main()
        {
            try
            {
                // Without this the box would only hear the keyboard while the game has focus, which
                // in borderless windowed mode is not where a player's attention always is.
                Game.AlwaysReceiveKeyEvents = true;

                _settings = Settings.Load();
                _settings.Resolve();

                _chat = new ChatBox();
                _api = new LspdfrApi();
                _dispatch = new DispatchService(_chat, _api, _settings);
                _dialogue = new DialogueService(_chat, _api, _settings);
                _router = new CommandRouter(_chat, _api, _dispatch, _dialogue);
                _input = new TextInput(_chat, _router.Handle);

                if (!_input.SetOpenKey(_settings.OpenKey))
                    Log.Line("OpenKey '" + _settings.OpenKey + "' is not a key name; sticking with T");

                Game.FrameRender += OnFrameRender;
                Game.AddConsoleCommands(new Type[] { typeof(ConsoleCommands) });

                Log.Line("starting; " + _api.Describe() + "; npc speech mode=" + _settings.AiMode);

                _chat.Notice("TextDispatch loaded. Press " + _settings.OpenKey + " to chat - /help for the commands.");
                _chat.Notice("Type anything to speak out loud. People nearby answer in text (" +
                             (_settings.UseModel ? "model: " + _settings.AiProvider : "scripted") + " mode).");
                _chat.Notice("Type in the box with /r <text> to talk to dispatch - it answers on the radio.");
                _chat.Dispatch("Dispatch online. " + _dispatch.Unit +
                               ", you are 10-8. I will call you when something comes in.");

                // Pull the model into memory now rather than on the player's first sentence.
                Ai.LocalModel.WarmUp(_settings);

                while (true)
                {
                    try
                    {
                        _input.Update();

                        // While the box is open the player is typing, not driving. Freezing the game's
                        // controls is what makes typing "10-97" not also steer the car.
                        if (_chat.IsOpen) DisableGameControls();

                        _dispatch.Update();
                        _dialogue.Update();
                    }
                    catch (Exception ex) { Log.Error("tick", ex); }

                    // Removing this would hang GTA V permanently: RPH runs plugins on fibers.
                    GameFiber.Yield();
                }
            }
            catch (Exception ex) { Log.Error("startup", ex); }
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
            try
            {
                _chat.Render(e.Graphics);
            }
            catch (Exception ex)
            {
                if (!_renderErrorLogged)
                {
                    _renderErrorLogged = true;
                    Log.Error("render", ex);
                }
            }
            finally { _rendering = false; }
        }
    }
}
