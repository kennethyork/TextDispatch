using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TextDispatch.Chat
{
    /// <summary>
    /// The Windows calls this plugin makes itself.
    ///
    /// They exist for one reason: to keep what the player types away from the *other* plugins.
    /// Those plugins read the keyboard through RAGE Plugin Hook, and RPH has no way to say "not
    /// while he is typing" - so the keystroke has to be taken out of the pipe before it reaches
    /// them. There are two places it can be caught on the way in: the system-wide low-level hook
    /// chain, and the game window's own message procedure.
    ///
    /// Everything here is declared but never trusted: every call site checks, because a plugin that
    /// gets these wrong takes the game's input down with it.
    /// </summary>
    internal static class Win32
    {
        // ---------------------------------------------------------------- the low-level hook

        public const int WH_KEYBOARD_LL = 13;
        public const int HC_ACTION = 0;

        public const uint LLKHF_INJECTED = 0x10;
        public const uint LLKHF_ALTDOWN = 0x20;
        public const uint LLKHF_UP = 0x80;

        [StructLayout(LayoutKind.Sequential)]
        public struct KbdHookData
        {
            public uint VirtualKey;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int hook, LowLevelKeyboardProc callback, IntPtr module, uint thread);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        // ---------------------------------------------------------------- the game window

        public const int GWL_WNDPROC = -4;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WM_INPUT = 0x00FF;
        public const uint WM_QUIT = 0x0012;

        public const uint RID_INPUT = 0x10000003;
        public const uint RIM_TYPEKEYBOARD = 1;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate IntPtr WindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputHeader
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr Param;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int index);

        /// <summary>The window procedure currently installed on a window, or zero.</summary>
        public static IntPtr GetWindowProcedure(IntPtr hWnd)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, GWL_WNDPROC) : GetWindowLong32(hWnd, GWL_WNDPROC);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int index, IntPtr value);

        /// <summary>Replace a window's procedure, returning the one that was there.</summary>
        public static IntPtr SetWindowProcedure(IntPtr hWnd, IntPtr value)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, GWL_WNDPROC, value) : SetWindowLong32(hWnd, GWL_WNDPROC, value);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int key);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int maximum);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        /// <summary>
        /// GTA's own window class. Used to recognise the game window if the process reports no main
        /// window, which is what happens when the game is running in borderless windowed mode.
        /// </summary>
        public const string GameWindowClass = "grcWindow";

        // ---------------------------------------------------------------- synthetic keys

        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// Stamped on every key this plugin synthesises, so its own hook can tell them apart from the
        /// player's. Only a plugin's own injected keys carry this.
        /// </summary>
        public static readonly IntPtr ProbeMark = new IntPtr(0x5444);      // 'TD'

        /// <summary>Stamped on the release that follows a hidden press, for the same reason.</summary>
        public static readonly IntPtr HideMark = new IntPtr(0x5445);

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput
        {
            public int X;
            public int Y;
            public uint Data;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HardwareInput
        {
            public uint Message;
            public ushort ParamLow;
            public ushort ParamHigh;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public KeyboardInput Keyboard;
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public HardwareInput Hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Input
        {
            public uint Type;
            public InputUnion Union;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint count, Input[] inputs, int size);

        /// <summary>Press or release a key on the player's behalf, marked as ours.</summary>
        public static bool SendKey(int virtualKey, bool up, IntPtr mark)
        {
            try
            {
                var inputs = new Input[1];
                inputs[0].Type = INPUT_KEYBOARD;
                inputs[0].Union.Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)virtualKey,
                    Flags = up ? KEYEVENTF_KEYUP : 0,
                    ExtraInfo = mark
                };
                return SendInput(1, inputs, Marshal.SizeOf(typeof(Input))) == 1;
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------- the hook thread's pump

        [StructLayout(LayoutKind.Sequential)]
        public struct Message
        {
            public IntPtr Window;
            public uint Id;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint Time;
            public int PointX;
            public int PointY;
        }

        [DllImport("user32.dll")]
        public static extern int GetMessage(out Message message, IntPtr window, uint minimum, uint maximum);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref Message message);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref Message message);

        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string name);

        // ---------------------------------------------------------------- key codes

        public const int VK_F24 = 0x87;             // the probe key: no game and no menu uses it
        public const int VK_CAPITAL = 0x14;
        public const int VK_SHIFT = 0x10;
        public const int VK_LSHIFT = 0xA0;
        public const int VK_RSHIFT = 0xA1;
        public const int VK_CONTROL = 0x11;
        public const int VK_LCONTROL = 0xA2;
        public const int VK_RCONTROL = 0xA3;
    }
}
