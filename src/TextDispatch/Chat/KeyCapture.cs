using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TextDispatch.Chat
{
    /// <summary>
    /// Keeps what the player types away from everything else in the game.
    ///
    /// The problem it solves: while the chat box is open, the keystrokes that spell "10-97" are the
    /// same keystrokes other plugins watch for, so typing a sentence opens their menus. There is no
    /// RPH call that says "not while he is typing", so the keys have to be taken out of the pipe -
    /// by a system-wide keyboard hook, by the game window's own message procedure, or by both, which
    /// is what this does. Either one is enough to work; together they cover the ways a plugin can be
    /// reading the keyboard.
    ///
    /// Two things are deliberate:
    ///
    ///   * nothing is hidden unless the box is open *and* the game window is in front. Typing in
    ///     another window on another monitor must never be touched, which on this install is how the
    ///     player looks things up;
    ///   * the plugin checks whether it is actually working, rather than assuming. Once, the first
    ///     time the box is opened, it hides a key of its own and then asks the game whether it can
    ///     still see that key. The answer says plainly whether other plugins are blind to typing,
    ///     and it is written to the log and shown by /block.
    /// </summary>
    internal sealed class KeyCapture
    {
        private readonly KeyboardFilter _filter = new KeyboardFilter();
        private readonly ConcurrentQueue<KeyPress> _presses = new ConcurrentQueue<KeyPress>();
        private readonly KeyboardHook _keyboard;
        private readonly WindowHook _window;
        private readonly Func<Keys, bool> _gameSeesKey;

        private IntPtr _gameWindow = IntPtr.Zero;
        private string _windowDescription = "not looked for";
        private bool _keyboardHook = false;
        private string _note = "not started";

        // The self-check, spread over two ticks: press, then read while it is still held.
        private int _probeStage;
        private int _probeAtTick;
        private string _probe = "not run yet";

        public KeyCapture(Func<Keys, bool> gameSeesKey)
        {
            _gameSeesKey = gameSeesKey;
            _keyboard = new KeyboardHook(_filter, _presses);
            _window = new WindowHook(_filter, _presses);
        }

        /// <summary>True when at least one of the two hooks is installed, so typing comes from them.</summary>
        public bool Active { get { return _keyboardHook || _window.Installed; } }

        /// <summary>Where the box's typing comes from, in one line, for the log and for /block.</summary>
        public string Note { get { return _note; } }

        public string Probe { get { return _probe; } }

        // ------------------------------------------------------------------ start and stop

        public void Start()
        {
            // The window first: it is what tells "typing into the game" from "typing into something
            // else", and both hooks consult it.
            _gameWindow = WindowHook.Find(out _windowDescription);

            _keyboard.Start();
            _keyboardHook = _keyboard.SelfTest();

            if (_keyboardHook)
            {
                _note = "the system-wide keyboard hook, installed and confirmed by a key of its own";
            }
            else
            {
                // Leave it installed: it costs nothing and may yet work. What is not trusted is
                // whether it is being called, and typing falls back to reading the keyboard as the
                // plugin did before any of this existed.
                _note = _keyboard.Installed
                    ? "a keyboard hook that Windows accepted but never called - typing falls back to reading the keyboard, and other plugins will still see it"
                    : "no keyboard hook - typing falls back to reading the keyboard, and other plugins will still see it";
            }

            if (_gameWindow != IntPtr.Zero)
            {
                if (_window.Install(_gameWindow))
                    _note += "; the game window's messages are hooked too";
                else
                    _note += "; the game window's messages could not be hooked";
            }
            else
            {
                _note += "; the game window was not found, so nothing can be hidden while another window is in front";
            }

            Log.Line("keys: window " + _windowDescription);
            Log.Line("keys: " + _note);
        }

        public void Stop()
        {
            _window.Uninstall();
            _keyboard.Stop();
        }

        // ------------------------------------------------------------------ per tick

        /// <summary>Called every tick with whether the box is open.</summary>
        public void Update(bool boxOpen)
        {
            bool gameInFront = false;

            try
            {
                gameInFront = _gameWindow != IntPtr.Zero && Win32.GetForegroundWindow() == _gameWindow;
            }
            catch { }

            _filter.Capturing = boxOpen && gameInFront && Active;

            // The window hook only feeds the box while the system-wide one is not doing it, or every
            // keystroke would arrive twice.
            _window.FeedEvents = !_keyboardHook;

            SelfCheck();
        }

        /// <summary>Take everything typed since the last tick. Empty when the hooks are not in use.</summary>
        public void Drain(List<KeyPress> into)
        {
            KeyPress press;
            while (_presses.TryDequeue(out press)) into.Add(press);
        }

        // ------------------------------------------------------------------ settings

        public void SetEnabled(bool on)
        {
            _filter.Active = on;
            if (!on) _filter.Capturing = false;
        }

        public bool Enabled { get { return _filter.Active; } }

        public void SetHardware(bool on) { _filter.HideFromHardware = on; }

        public bool Hardware { get { return _filter.HideFromHardware; } }

        /// <summary>What /block says: whether it is on, what got installed, and what the test found.</summary>
        public string Describe()
        {
            return "Hiding what you type from other plugins is " + (_filter.Active ? "on" : "off") + ". " + _note +
                   ". Reading the hardware directly is " + (_filter.HideFromHardware ? "hidden too" : "not hidden") + ".";
        }

        // ------------------------------------------------------------------ the self-check

        /// <summary>
        /// Hide one key of our own and ask the game whether it can still see it.
        ///
        /// This is the only way to know whether the hiding works, and it cannot be answered from
        /// outside the game: the hooks are in this process and the readers are in this process, but
        /// which of them asks the window messages and which asks the hardware is RAGE Plugin Hook's
        /// business, not ours. If the probe key stays invisible, the plugins that read the keyboard
        /// through RPH are blind to typing; if it is visible, they are reading the hardware, and the
        /// synthetic release (HideFromHardware) is what covers them instead.
        /// </summary>
        private void SelfCheck()
        {
            if (_probeStage == 2 || !_filter.Capturing || !Active) return;

            if (_probeStage == 0)
            {
                _probeStage = 1;
                _probeAtTick = Environment.TickCount;
                _filter.SwallowInjected = true;
                _keyboard.TapProbe();
                return;
            }

            if (Environment.TickCount - _probeAtTick < 150) return;

            bool seen = false;
            try { seen = _gameSeesKey((Keys)_keyboard.ProbeKey); } catch { }

            _keyboard.ReleaseProbe();
            _filter.SwallowInjected = false;
            _probeStage = 2;

            _probe = seen
                ? "a key hidden from the game was still visible to it, so a plugin reading the keyboard hardware directly would still have seen it - that is what the synthetic release is for"
                : "a key hidden from the game was invisible to it, so other plugins do not see what is typed";

            Log.Line("keys: self-check - " + _probe);
        }
    }
}
