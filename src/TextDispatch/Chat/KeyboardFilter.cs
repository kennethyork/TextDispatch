using System.Collections.Generic;
using System.Windows.Forms;

namespace TextDispatch.Chat
{
    /// <summary>One keystroke, with the modifier state as it was at that instant.</summary>
    internal struct KeyPress
    {
        public Keys Key;
        public bool Shift;
        public bool Control;
        public bool Caps;
    }

    /// <summary>
    /// The rule for what reaches the game and the other plugins while the player is typing.
    ///
    /// Deliberately free of Windows calls, so the rule can be tried on its own rather than only
    /// discovered in game: everything it needs is handed to it.
    ///
    /// The default is to hide everything, because that is the point - typing "10-97" must not open
    /// somebody else's menu. Three things are always let through:
    ///
    ///   * Alt with anything, or Alt+Tab stops working and the player cannot leave the game;
    ///   * a release whose press was let through, because swallowing it would leave the game
    ///     believing a key is still held down - a stuck key is worse than the problem being fixed;
    ///   * anything another program synthesised, since that is not the player typing.
    /// </summary>
    internal sealed class KeyboardFilter
    {
        /// <summary>True while the player is typing: box open, game in front, and switched on.</summary>
        public bool Capturing;

        /// <summary>Set to false by /block off, for a player who wants the old behaviour back.</summary>
        public bool Active = true;

        /// <summary>
        /// True while this plugin is injecting its own probe key. The probe has to be swallowed too,
        /// or testing itself would be the one thing that still types into the game.
        /// </summary>
        public bool SwallowInjected;

        /// <summary>
        /// Follow every hidden press with a synthetic release.
        ///
        /// Blocking the key where it is caught hides it from everything that reads the game's
        /// messages. It does not hide it from a reader that asks the hardware directly, and some
        /// plugins do exactly that. Releasing the key immediately empties the hardware state too, so
        /// a reader polling it sees a key that went down and up inside the same frame - too fast for
        /// any menu to open on, and the game's own controls are disabled while the box is open
        /// anyway. It is also why nothing here is left to chance: it can be turned off with
        /// /block hardware off.
        /// </summary>
        public bool HideFromHardware = true;

        private readonly HashSet<int> _hidden = new HashSet<int>();

        public bool Shift { get; private set; }
        public bool Control { get; private set; }

        /// <summary>Read from Windows, which is unaffected by anything hidden here.</summary>
        public bool CapsLock
        {
            get
            {
                try { return (Win32.GetKeyState(Win32.VK_CAPITAL) & 1) != 0; }
                catch { return false; }
            }
        }

        /// <summary>
        /// Decide one keystroke. Returns true when it must not reach anything else.
        /// </summary>
        public bool Swallow(int virtualKey, bool down, bool injected, bool altDown)
        {
            Track(virtualKey, down);

            if (!Capturing || !Active) return false;
            if (injected && !SwallowInjected) return false;
            if (altDown) return false;

            if (down)
            {
                _hidden.Add(virtualKey);
                return true;
            }

            return _hidden.Remove(virtualKey);
        }

        /// <summary>The keystroke as the box wants it: which key, and how it was modified.</summary>
        public KeyPress Press(int virtualKey)
        {
            return new KeyPress
            {
                Key = (Keys)virtualKey,
                Shift = Shift,
                Control = Control,
                Caps = CapsLock
            };
        }

        /// <summary>
        /// Caps lock aside, the modifiers are tracked here rather than asked of Windows, because
        /// shift is one of the keys being hidden - a reader that no longer sees shift down would
        /// otherwise capitalise nothing.
        /// </summary>
        private void Track(int virtualKey, bool down)
        {
            switch (virtualKey)
            {
                case Win32.VK_SHIFT:
                case Win32.VK_LSHIFT:
                case Win32.VK_RSHIFT:
                    Shift = down;
                    break;

                case Win32.VK_CONTROL:
                case Win32.VK_LCONTROL:
                case Win32.VK_RCONTROL:
                    Control = down;
                    break;
            }
        }
    }
}
