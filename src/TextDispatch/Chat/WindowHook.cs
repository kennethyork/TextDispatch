using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TextDispatch.Chat
{
    /// <summary>
    /// A hook on the game window's own message procedure - the second place a keystroke can be
    /// caught, and the only one that can stop a reader using the window's raw input.
    ///
    /// It is worth having both this and the system-wide hook. The system-wide one is called first
    /// and catches everything, but raw keyboard reports (WM_INPUT) do not travel through it. The
    /// window procedure does see them, because that is where Windows delivers them, so dropping the
    /// keyboard ones here covers a reader that asks for raw input rather than for key messages.
    ///
    /// Chaining is the whole risk: another program - or RAGE Plugin Hook itself - may already have
    /// replaced this window's procedure, and every message that is not a keystroke being hidden has
    /// to be handed straight on to it, unchanged and in order.
    /// </summary>
    internal sealed class WindowHook
    {
        private readonly KeyboardFilter _filter;
        private readonly ConcurrentQueue<KeyPress> _presses;

        // Both of these are pointers the window holds: the delegate must outlive the hook.
        private Win32.WindowProc _procedure;
        private IntPtr _previous = IntPtr.Zero;

        private IntPtr _window = IntPtr.Zero;
        private IntPtr _rawBuffer = IntPtr.Zero;
        private const int RawBufferSize = 128;

        public WindowHook(KeyboardFilter filter, ConcurrentQueue<KeyPress> presses)
        {
            _filter = filter;
            _presses = presses;
        }

        public bool Installed { get { return _window != IntPtr.Zero && _previous != IntPtr.Zero; } }

        /// <summary>
        /// Whether this hook feeds the box's typing. False while the system-wide hook is doing it:
        /// both would otherwise deliver every keystroke and each letter would be typed twice.
        /// </summary>
        public bool FeedEvents = true;

        public bool Install(IntPtr window)
        {
            if (Installed) return true;
            if (window == IntPtr.Zero) return false;

            try
            {
                _procedure = Procedure;
                _previous = Win32.GetWindowProcedure(window);
                if (_previous == IntPtr.Zero) return false;

                var replaced = Win32.SetWindowProcedure(window, Marshal.GetFunctionPointerForDelegate(_procedure));
                if (replaced == IntPtr.Zero) return false;

                _window = window;
                _rawBuffer = Marshal.AllocHGlobal(RawBufferSize);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("window hook", ex);
                return false;
            }
        }

        public void Uninstall()
        {
            try
            {
                if (_window != IntPtr.Zero && _previous != IntPtr.Zero)
                    Win32.SetWindowProcedure(_window, _previous);
            }
            catch { }

            try
            {
                if (_rawBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_rawBuffer);
            }
            catch { }

            _rawBuffer = IntPtr.Zero;
            _window = IntPtr.Zero;
            _previous = IntPtr.Zero;
        }

        private IntPtr Procedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                switch (message)
                {
                    case Win32.WM_KEYDOWN:
                    case Win32.WM_SYSKEYDOWN:
                    case Win32.WM_KEYUP:
                    case Win32.WM_SYSKEYUP:
                    {
                        int virtualKey = wParam.ToInt32();
                        bool down = message == Win32.WM_KEYDOWN || message == Win32.WM_SYSKEYDOWN;

                        // The SYS variants are the ones Windows sends while Alt is held, which is
                        // exactly the case that has to be let through.
                        bool altDown = message == Win32.WM_SYSKEYDOWN || message == Win32.WM_SYSKEYUP;

                        bool hide = _filter.Swallow(virtualKey, down, false, altDown);

                        if (FeedEvents && down) _presses.Enqueue(_filter.Press(virtualKey));
                        if (hide) return IntPtr.Zero;
                        break;
                    }

                    case Win32.WM_INPUT:
                        if (KeyboardReport(lParam)) return IntPtr.Zero;
                        break;
                }
            }
            catch { }

            return Win32.CallWindowProc(_previous, window, message, wParam, lParam);
        }

        /// <summary>
        /// Whether a raw input report came from a keyboard, in which case it is dropped while the
        /// player is typing.
        ///
        /// Only the header is read, and only while typing: this is called for every mouse movement,
        /// and a camera that stops turning because a plugin swallowed its reports would be a far
        /// worse bug than the one being fixed.
        /// </summary>
        private bool KeyboardReport(IntPtr report)
        {
            if (!_filter.Capturing || !_filter.Active) return false;
            if (_rawBuffer == IntPtr.Zero) return false;

            try
            {
                uint size = RawBufferSize;
                uint written = Win32.GetRawInputData(report, Win32.RID_INPUT, _rawBuffer, ref size,
                                                     (uint)Marshal.SizeOf(typeof(Win32.RawInputHeader)));

                if (written == 0 || written == uint.MaxValue) return false;

                var header = (Win32.RawInputHeader)Marshal.PtrToStructure(_rawBuffer, typeof(Win32.RawInputHeader));
                return header.Type == Win32.RIM_TYPEKEYBOARD;
            }
            catch { return false; }
        }

        /// <summary>
        /// The game's own window, which is what "the game is in front" is measured against.
        ///
        /// The process is asked first, and if it will not name one - which is what happens in
        /// borderless windowed mode - the windows belonging to this process are walked instead, GTA's
        /// own window class first and then simply the largest visible one.
        /// </summary>
        public static IntPtr Find(out string description)
        {
            description = "not found";

            try
            {
                var main = Process.GetCurrentProcess().MainWindowHandle;
                if (main != IntPtr.Zero && Win32.GetWindowThreadProcessId(main, out uint _) != 0)
                {
                    description = Describe(main);
                    return main;
                }
            }
            catch { }

            try
            {
                uint mine = (uint)Process.GetCurrentProcess().Id;
                IntPtr best = IntPtr.Zero;
                long bestArea = -1;

                Win32.EnumWindows(delegate (IntPtr window, IntPtr parameter)
                {
                    try
                    {
                        if (!Win32.IsWindowVisible(window)) return true;

                        Win32.GetWindowThreadProcessId(window, out uint owner);
                        if (owner != mine) return true;

                        var name = new StringBuilder(256);
                        Win32.GetClassName(window, name, name.Capacity);
                        bool game = string.Equals(name.ToString(), Win32.GameWindowClass, StringComparison.OrdinalIgnoreCase);

                        var rect = new Win32.Rect();
                        Win32.GetWindowRect(window, out rect);
                        long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);

                        // GTA's own class wins outright; anything else only by being the biggest.
                        if (game) { best = window; bestArea = long.MaxValue; }
                        else if (bestArea != long.MaxValue && area > bestArea) { best = window; bestArea = area; }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);

                if (best != IntPtr.Zero)
                {
                    description = Describe(best) + " (by class, the process named no main window)";
                    return best;
                }
            }
            catch { }

            return IntPtr.Zero;
        }

        private static string Describe(IntPtr window)
        {
            try
            {
                var name = new StringBuilder(256);
                Win32.GetClassName(window, name, name.Capacity);
                var rect = new Win32.Rect();
                Win32.GetWindowRect(window, out rect);
                return "0x" + window.ToInt64().ToString("X") + " class '" + name + "' " +
                       (rect.Right - rect.Left) + "x" + (rect.Bottom - rect.Top);
            }
            catch { return "0x" + window.ToInt64().ToString("X"); }
        }
    }
}
