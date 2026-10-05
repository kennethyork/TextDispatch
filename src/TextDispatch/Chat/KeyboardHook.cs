using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace TextDispatch.Chat
{
    /// <summary>
    /// The system-wide low-level keyboard hook.
    ///
    /// This is the copy of every keystroke in the process that no plugin can get behind: a hook
    /// installed later than one already there is called earlier, and RAGE Plugin Hook installs its
    /// own long before any plugin loads. So while the box is open, keys are dropped here and never
    /// reach RPH's keyboard state at all - which is what stops the other plugins reacting to them.
    ///
    /// It runs on a thread of its own with its own message pump, for two reasons: Windows delivers
    /// low-level hooks on the thread that installed them and expects that thread to pump messages,
    /// and keeping it off the game's thread means a slow callback here cannot stall a frame. Windows
    /// silently removes a low-level hook whose callback takes too long, so the callback does nothing
    /// but decide and record.
    /// </summary>
    internal sealed class KeyboardHook
    {
        private readonly KeyboardFilter _filter;
        private readonly ConcurrentQueue<KeyPress> _presses;

        // The OS holds this function pointer for as long as the hook is installed: if the delegate
        // were collected, the hook would call freed memory and take the game with it.
        private Win32.LowLevelKeyboardProc _callback;

        private IntPtr _hook = IntPtr.Zero;
        private Thread _thread;
        private uint _threadId;

        private volatile bool _stopping;
        private int _events;
        private int _probeEvents;

        public KeyboardHook(KeyboardFilter filter, ConcurrentQueue<KeyPress> presses)
        {
            _filter = filter;
            _presses = presses;
        }

        public bool Installed { get { return _hook != IntPtr.Zero; } }

        internal int ProbeEvents { get { return Thread.VolatileRead(ref _probeEvents); } }

        /// <summary>
        /// Start the thread and install the hook. Returns quickly either way; the caller checks
        /// Installed, and works without it if it is false.
        /// </summary>
        public void Start()
        {
            if (_thread != null) return;

            _thread = new Thread(Pump);
            _thread.IsBackground = true;
            _thread.Name = "TextDispatch keyboard hook";
            _thread.Start();

            for (int waited = 0; waited < 200 && _hook == IntPtr.Zero && !_stopping; waited++)
                Thread.Sleep(5);
        }

        public void Stop()
        {
            _stopping = true;

            try
            {
                if (_threadId != 0) Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }

            try
            {
                // Unhooking has to happen on the thread that installed it, so the pump does it on the
                // way out. This is the backstop, for a pump that never got as far as starting.
                if (_thread == null && _hook != IntPtr.Zero)
                {
                    Win32.UnhookWindowsHookEx(_hook);
                    _hook = IntPtr.Zero;
                }
            }
            catch { }
        }

        /// <summary>
        /// Prove the hook is not just installed but actually being called, by sending it a key of its
        /// own and waiting to see it come back. A hook that is installed and silent - because the
        /// thread it was installed on does not pump messages - would look exactly like a working one
        /// while the player's typing went nowhere.
        /// </summary>
        public bool SelfTest()
        {
            if (_hook == IntPtr.Zero) return false;

            try
            {
                Win32.SendKey(Win32.VK_F24, false, Win32.ProbeMark);
                for (int waited = 0; waited < 400 && ProbeEvents == 0; waited++) Thread.Sleep(5);
                Win32.SendKey(Win32.VK_F24, true, Win32.ProbeMark);
                return ProbeEvents > 0;
            }
            catch { return false; }
        }

        private void Pump()
        {
            _threadId = Win32.GetCurrentThreadId();

            try
            {
                _callback = Callback;
                _hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _callback, Win32.GetModuleHandle(null), 0);

                if (_hook == IntPtr.Zero)
                {
                    Log.Line("keyboard hook: Windows refused it (error " + Marshal.GetLastWin32Error() +
                             ") - other plugins will still see what you type");
                    return;
                }

                Win32.Message message;
                while (!_stopping && Win32.GetMessage(out message, IntPtr.Zero, 0, 0) > 0)
                {
                    Win32.TranslateMessage(ref message);
                    Win32.DispatchMessage(ref message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("keyboard hook", ex);
            }
            finally
            {
                try
                {
                    if (_hook != IntPtr.Zero)
                    {
                        Win32.UnhookWindowsHookEx(_hook);
                        _hook = IntPtr.Zero;
                    }
                }
                catch { }
            }
        }

        private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code == Win32.HC_ACTION)
                {
                    var data = (Win32.KbdHookData)Marshal.PtrToStructure(lParam, typeof(Win32.KbdHookData));

                    int virtualKey = (int)data.VirtualKey;
                    bool down = (data.Flags & Win32.LLKHF_UP) == 0;
                    bool injected = (data.Flags & Win32.LLKHF_INJECTED) != 0;
                    bool altDown = (data.Flags & Win32.LLKHF_ALTDOWN) != 0;
                    bool mine = (data.ExtraInfo != IntPtr.Zero && data.ExtraInfo == Win32.ProbeMark) ||
                                (data.ExtraInfo != IntPtr.Zero && data.ExtraInfo == Win32.HideMark);

                    Interlocked.Increment(ref _events);
                    if (mine) Interlocked.Increment(ref _probeEvents);

                    bool hide = _filter.Swallow(virtualKey, down, injected, altDown);

                    // The box's own typing comes from here: presses only, and never a synthetic one,
                    // or the probe would appear in what the player had written.
                    if (down && !injected) _presses.Enqueue(_filter.Press(virtualKey));

                    if (hide)
                    {
                        // Empty the hardware state as well, unless this press was already ours.
                        if (_filter.HideFromHardware && down && !mine) Hide(virtualKey);

                        // Non-zero means consumed: no later hook is called and the window never sees it.
                        return new IntPtr(1);
                    }
                }
            }
            catch { }

            return Win32.CallNextHookEx(_hook, code, wParam, lParam);
        }

        /// <summary>Release a key that was just hidden, so nothing polling the hardware can find it down.</summary>
        private void Hide(int virtualKey)
        {
            try { Win32.SendKey(virtualKey, true, Win32.HideMark); }
            catch { }
        }

        /// <summary>Press the probe key, to be swallowed on the way out. Used to test hiding.</summary>
        public void TapProbe()
        {
            try { Win32.SendKey(Win32.VK_F24, false, Win32.ProbeMark); }
            catch { }
        }

        /// <summary>The key the probe presses. The same one SelfTest uses, so the two cannot drift apart.</summary>
        public int ProbeKey { get { return Win32.VK_F24; } }

        public void ReleaseProbe()
        {
            try { Win32.SendKey(Win32.VK_F24, true, Win32.ProbeMark); }
            catch { }
        }
    }
}
