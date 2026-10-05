using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TextDispatch;
using TextDispatch.Chat;

namespace TextJobs
{
    /// <summary>
    /// Turns the keyboard into a line of text, and hands it over on Enter.
    ///
    /// The keystrokes come from the key capture - the same code TextDispatch uses - rather than from
    /// ScriptHookVDotNet's key events, and that is the point of it: while the box is open those
    /// keystrokes are also being hidden from every other plugin and script, including DriverJobs'
    /// own Shift+J, so typing "San Andreas Freight" cannot open somebody else's menu.
    /// </summary>
    internal sealed class TypedInput
    {
        private readonly ChatBox _box;
        private readonly Action<string> _submit;
        private readonly KeyCapture _capture;
        private readonly List<KeyPress> _typed = new List<KeyPress>();
        private readonly List<Keys> _openKeys = new List<Keys> { Keys.F8 };

        public TypedInput(ChatBox box, Action<string> submit, KeyCapture capture)
        {
            _box = box;
            _submit = submit;
            _capture = capture;
        }

        /// <summary>Any System.Windows.Forms.Keys name: F8, Right, Numpad0, OemQuestion.</summary>
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

        public string OpenKeyDescription
        {
            get
            {
                var parts = new List<string>();
                foreach (var key in _openKeys) parts.Add(key.ToString());
                return string.Join(" or ", parts.ToArray());
            }
        }

        public void Update()
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
            if (!_box.IsOpen)
            {
                if (_openKeys.Contains(key)) { _box.IsOpen = true; _box.Input = ""; }
                return;
            }

            switch (key)
            {
                case Keys.Escape:
                    _box.IsOpen = false;
                    _box.Input = "";
                    return;

                case Keys.Enter:
                {
                    var text = _box.Input;
                    _box.IsOpen = false;
                    _box.Input = "";
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _box.Remember(text);
                        _submit(text);
                    }
                    return;
                }

                case Keys.Back:
                    if (_box.Input.Length > 0) _box.Input = _box.Input.Substring(0, _box.Input.Length - 1);
                    return;

                case Keys.Up: _box.HistoryPrev(); return;
                case Keys.Down: _box.HistoryNext(); return;
                case Keys.Tab: return;
            }

            if (control) return;   // no pasting here: this box takes job names, not paragraphs

            var character = CharacterFor(key, shift, caps);
            if (character != '\0') _box.Input += character;
        }

        /// <summary>
        /// The same mapping TextDispatch uses, and small on purpose: a job name is letters, digits and
        /// a little punctuation, and none of it is worth a keyboard-layout library.
        /// </summary>
        private static char CharacterFor(Keys key, bool shift, bool caps)
        {
            if (key == Keys.Space) return ' ';

            if (key >= Keys.A && key <= Keys.Z)
            {
                var lower = (char)('a' + (key - Keys.A));
                return (shift ^ caps) ? char.ToUpperInvariant(lower) : lower;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                const string plain = "0123456789";
                const string shifted = ")!@#$%^&*(";
                var index = key - Keys.D0;
                return shift ? shifted[index] : plain[index];
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));

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
    }
}
