using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Rage;

namespace TextDispatch.Chat
{
    /// <summary>
    /// Turns the keyboard into a line of text.
    ///
    /// RPH hands over a keyboard *state* (which keys are down right now), not typed characters, so
    /// this polls it once per fiber tick and derives edges itself: a key that is down now and was
    /// not down last tick is a keystroke. Doing our own edge detection keeps it independent of how
    /// the host defines "pressed".
    ///
    /// There are two sources, and the second exists because the first stops working exactly when it
    /// matters. While the key capture is running - which is whenever the box is open - the keystrokes
    /// come from it instead, because that is also where they are being hidden from the other plugins,
    /// and a key hidden on its way into the game never reaches RPH's keyboard state for this to poll.
    /// When no hook could be installed the polling below is still here, unchanged, so the box keeps
    /// working on an install where the hooks are refused.
    /// </summary>
    public sealed class TextInput
    {
        private readonly ChatBox _chat;
        private readonly Action<string> _submit;
        private readonly KeyCapture _capture;
        private readonly List<KeyPress> _typed = new List<KeyPress>();
        private readonly Dictionary<Keys, bool> _wasDown = new Dictionary<Keys, bool>();
        private static readonly Keys[] Scan = BuildScanKeys();
        /// <summary>
        /// Every key that opens the box - more than one because a single key is a single point of
        /// failure. On this install T is already claimed by something else, and a chat box that
        /// cannot be opened is not much of a chat box.
        /// </summary>
        private readonly List<Keys> _openKeys = new List<Keys> { Keys.F6 };

        // Internal, unlike the rest of this class: the key capture it is handed is an implementation
        // detail of the plugin, not something another plugin could ever be given.
        internal TextInput(ChatBox chat, Action<string> submit, KeyCapture capture)
        {
            _chat = chat;
            _submit = submit;
            _capture = capture;
        }

        /// <summary>
        /// Change which key opens the box. Accepts any System.Windows.Forms.Keys name, so "T", "F6",
        /// "Home" and "OemQuestion" all work. Returns false and keeps the old key if the name is not
        /// a key, because silently keeping a working box beats silently breaking it.
        /// </summary>
        public bool SetOpenKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var wanted = new List<Keys>();
            foreach (var part in name.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Keys parsed;
                if (!Enum.TryParse(part.Trim(), true, out parsed)) return false;
                if (parsed == Keys.None) return false;
                if (!wanted.Contains(parsed)) wanted.Add(parsed);
            }

            if (wanted.Count == 0) return false;

            _openKeys.Clear();
            _openKeys.AddRange(wanted);
            return true;
        }

        /// <summary>'F6', or 'T or F6' - for saying back what the box opens with.</summary>
        public string OpenKeyDescription
        {
            get
            {
                var parts = new List<string>();
                foreach (var key in _openKeys) parts.Add(key.ToString());
                return string.Join(" or ", parts.ToArray());
            }
        }

        /// <summary>Whether controls should be frozen this frame because the player is typing.</summary>
        public bool Typing { get { return _chat.IsOpen; } }

        public void Update()
        {
            if (_capture != null && _capture.Active) { Captured(); return; }

            KeyboardState state;
            try { state = Game.GetKeyboardState(); }
            catch { return; }

            for (int i = 0; i < Scan.Length; i++)
            {
                var key = Scan[i];
                bool down = state.IsDown(key);
                bool was;
                _wasDown.TryGetValue(key, out was);
                _wasDown[key] = down;
                if (!down || was) continue;
                try { Pressed(key, state.IsShiftDown, state.IsControlDown, state.IsCapsLockDown); }
                catch (Exception ex) { Log.Error("input", ex); }
            }
        }

        /// <summary>
        /// Take the keystrokes the capture has collected. Only presses arrive, and only real ones:
        /// the probes and the synthetic releases it makes for its own purposes are marked and never
        /// reach here, so nothing it does to hide a key can end up in what the player wrote.
        /// </summary>
        private void Captured()
        {
            _typed.Clear();
            _capture.Drain(_typed);

            for (int i = 0; i < _typed.Count; i++)
            {
                var press = _typed[i];
                try { Pressed(press.Key, press.Shift, press.Control, press.Caps); }
                catch (Exception ex) { Log.Error("input", ex); }
            }
        }

        private void Pressed(Keys key, bool shift, bool control, bool caps)
        {
            // Closed: the open key brings the box up, "/" brings it up mid-command.
            if (!_chat.IsOpen)
            {
                if (_openKeys.Contains(key)) { _chat.IsOpen = true; _chat.Input = ""; _chat.Scroll = 0; }
                else if (key == Keys.OemQuestion) { _chat.IsOpen = true; _chat.Input = "/"; _chat.Scroll = 0; }
                return;
            }

            switch (key)
            {
                case Keys.Escape:
                    _chat.IsOpen = false;
                    _chat.Input = "";
                    return;

                case Keys.Enter:
                {
                    var text = _chat.Input;
                    _chat.IsOpen = false;
                    _chat.Input = "";
                    _chat.Scroll = 0;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _chat.Remember(text);
                        _submit(text);
                    }
                    return;
                }

                case Keys.Back:
                    if (_chat.Input.Length > 0) _chat.Input = _chat.Input.Substring(0, _chat.Input.Length - 1);
                    return;

                case Keys.Up: _chat.HistoryPrev(); return;
                case Keys.Down: _chat.HistoryNext(); return;
                case Keys.PageUp: _chat.Scroll += 3; return;
                case Keys.PageDown: _chat.Scroll = Math.Max(0, _chat.Scroll - 3); return;
                case Keys.Tab: return;   // reserved for completion
            }

            // Everything else becomes text. Note the open key is only special while the box is
            // closed - once it is open, typing a 't' must insert a 't'.

            if (control)
            {
                // Ctrl+V pastes - the one shortcut worth having in a chat box.
                if (key == Keys.V)
                {
                    try { _chat.Input += Game.GetClipboardText(); } catch { }
                }
                return;
            }

            var ch = CharacterFor(key, shift, caps);
            if (ch != '\0')
            {
                _chat.Scroll = 0;
                _chat.Input += ch;
            }
        }

        private static char CharacterFor(Keys key, bool shift, bool caps)
        {
            if (key == Keys.Space) return ' ';

            if (key >= Keys.A && key <= Keys.Z)
            {
                char lower = (char)('a' + (key - Keys.A));
                bool upper = shift ^ caps;
                return upper ? char.ToUpperInvariant(lower) : lower;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                const string plain = "0123456789";
                const string shifted = ")!@#$%^&*(";
                int index = key - Keys.D0;
                return shift ? shifted[index] : plain[index];
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                return (char)('0' + (key - Keys.NumPad0));

            switch (key)
            {
                case Keys.OemPeriod: return shift ? '>' : '.';
                case Keys.Oemcomma: return shift ? '<' : ',';
                case Keys.OemMinus: return shift ? '_' : '-';
                case Keys.Oemplus: return shift ? '+' : '=';
                case Keys.OemQuestion: return shift ? '?' : '/';
                case Keys.OemSemicolon: return shift ? ':' : ';';
                case Keys.OemQuotes: return shift ? '"' : '\'';
                case Keys.OemOpenBrackets: return shift ? '{' : '[';
                case Keys.OemCloseBrackets: return shift ? '}' : ']';
                case Keys.OemBackslash: return shift ? '|' : '\\';
                case Keys.Oemtilde: return shift ? '~' : '`';
                case Keys.Multiply: return '*';
                case Keys.Add: return '+';
                case Keys.Subtract: return '-';
                case Keys.Divide: return '/';
                case Keys.Decimal: return '.';
            }

            return '\0';
        }

        private static Keys[] BuildScanKeys()
        {
            var keys = new List<Keys>();

            for (var k = Keys.A; k <= Keys.Z; k++) keys.Add(k);
            for (var k = Keys.D0; k <= Keys.D9; k++) keys.Add(k);
            for (var k = Keys.NumPad0; k <= Keys.NumPad9; k++) keys.Add(k);

            keys.AddRange(new[]
            {
                Keys.Space, Keys.OemPeriod, Keys.Oemcomma, Keys.OemMinus, Keys.Oemplus, Keys.OemQuestion,
                Keys.OemSemicolon, Keys.OemQuotes, Keys.OemOpenBrackets, Keys.OemCloseBrackets, Keys.OemBackslash,
                Keys.Oemtilde, Keys.Multiply, Keys.Add, Keys.Subtract, Keys.Divide,
                Keys.Decimal, Keys.Enter, Keys.Escape, Keys.Back, Keys.Up, Keys.Down,
                Keys.PageUp, Keys.PageDown, Keys.Tab
            });

            return keys.ToArray();
        }
    }
}
