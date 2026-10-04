using System;
using System.Runtime.InteropServices;

namespace CursorFree
{
    /// <summary>
    /// The whole mechanism, in one place: reading, setting and clearing the desktop cursor clip.
    ///
    /// When a game in borderless windowed mode has focus it calls ClipCursor() with its own window
    /// rectangle, and the mouse then cannot leave that rectangle - which on a multi-monitor desktop
    /// means it cannot reach any other screen. The clip is a desktop-wide setting, not a per-process
    /// one, so any process may change it back: ClipCursor(NULL) removes it entirely.
    ///
    /// Nothing here is speculative. GetClipCursor reports what the clip currently is, so both the
    /// tool and its test can see exactly what happened.
    /// </summary>
    public static class CursorGate
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }

            public override string ToString()
            {
                return "(" + Left + "," + Top + ")-(" + Right + "," + Bottom + ")  " + Width + "x" + Height;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(ref Rect rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(IntPtr none);

        [DllImport("user32.dll")]
        private static extern bool GetClipCursor(out Rect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        private const int VirtualScreenX = 76;
        private const int VirtualScreenY = 77;
        private const int VirtualScreenWidth = 78;
        private const int VirtualScreenHeight = 79;

        /// <summary>Every monitor, side by side. What the clip looks like when nothing is constrained.</summary>
        public static Rect Desktop()
        {
            return new Rect
            {
                Left = GetSystemMetrics(VirtualScreenX),
                Top = GetSystemMetrics(VirtualScreenY),
                Right = GetSystemMetrics(VirtualScreenX) + GetSystemMetrics(VirtualScreenWidth),
                Bottom = GetSystemMetrics(VirtualScreenY) + GetSystemMetrics(VirtualScreenHeight)
            };
        }

        /// <summary>What the cursor is currently confined to.</summary>
        public static Rect Current()
        {
            Rect rect;
            GetClipCursor(out rect);
            return rect;
        }

        /// <summary>
        /// True when the cursor is confined to something smaller than the desktop - which is the state
        /// a game leaves it in, and the state the player wants out of.
        /// </summary>
        public static bool IsConfined()
        {
            var clip = Current();
            var desktop = Desktop();
            return clip.Left > desktop.Left || clip.Top > desktop.Top ||
                   clip.Right < desktop.Right || clip.Bottom < desktop.Bottom ||
                   clip.Width < desktop.Width || clip.Height < desktop.Height;
        }

        /// <summary>Let the cursor go anywhere on the desktop.</summary>
        public static bool Free()
        {
            return ClipCursor(IntPtr.Zero);
        }

        /// <summary>Confine the cursor to a rectangle - how the clip is handed back.</summary>
        public static bool Confine(Rect rect)
        {
            return ClipCursor(ref rect);
        }

        /// <summary>
        /// Confine the cursor to whatever window is in front, which is the clip a game sets for itself.
        /// Returns false when the front window cannot be measured, so nothing is changed.
        /// </summary>
        public static bool ConfineToForegroundWindow(out Rect rect)
        {
            rect = new Rect();
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            if (!GetWindowRect(window, out rect)) return false;
            if (rect.Width <= 0 || rect.Height <= 0) return false;
            return Confine(rect);
        }
    }
}
