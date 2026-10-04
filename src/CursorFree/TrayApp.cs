using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CursorFree
{
    /// <summary>
    /// A tray icon that can take the cursor clip away from a fullscreen-ish game and hand it back.
    ///
    /// Why a toggle rather than "always free": while the clip is gone the mouse can wander off to
    /// another monitor mid-aim, which makes the game unusable. The useful thing is the ability to say
    /// "I want the mouse over there now" and then "I am playing again" - so the hotkey is the feature,
    /// and leaving the cursor loose at all times is deliberately not one.
    /// </summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private const int HotkeyId = 1;
        private const int WmHotkey = 0x0312;
        private const int ModAlt = 0x1;
        private const int ModControl = 0x2;
        private const int ModNoRepeat = 0x4000;
        private const int VkF = 0x46;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr window, int id, int modifiers, int key);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr window, int id);

        private sealed class MessageWindow : Form
        {
            public Action<int> Hotkey;

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == WmHotkey)
                {
                    var handler = Hotkey;
                    if (handler != null) handler(message.WParam.ToInt32());
                }

                base.WndProc(ref message);
            }
        }

        private readonly NotifyIcon _tray;
        private readonly Timer _watch;
        private readonly MessageWindow _window;
        private readonly ToolStripMenuItem _freeItem;
        private readonly string _log = Path.Combine(AppFolder(), "cursorfree.log");

        private bool _free;
        private int _reclips;
        private readonly bool _registered;

        public TrayApp()
        {
            _window = new MessageWindow { ShowInTaskbar = false, WindowState = FormWindowState.Minimized };
            var handle = _window.Handle;      // forces the window to exist so it can receive WM_HOTKEY

            _registered = RegisterHotKey(handle, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkF);
            _window.Hotkey = id => { if (id == HotkeyId) Toggle(); };

            _freeItem = new ToolStripMenuItem("Free the cursor  (Ctrl+Alt+F)") { CheckOnClick = false };
            _freeItem.Click += (s, e) => Toggle();

            var menu = new ContextMenuStrip();
            menu.Items.Add(_freeItem);
            menu.Items.Add("Hand it back to the front window", null, (s, e) => HandBack());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("What is the clip right now?", null, (s, e) => Report());
            menu.Items.Add("Open the log", null, (s, e) => OpenLog());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitThread());

            _tray = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "CursorFree - Ctrl+Alt+F frees the mouse from the game window",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += (s, e) => Toggle();

            // The game re-applies its clip whenever it has reason to, so being free is a condition to
            // keep asserting rather than a switch that stays thrown. 25 ms is under two frames.
            _watch = new Timer { Interval = 25 };
            _watch.Tick += (s, e) => KeepFree();
            _watch.Start();

            Line("started; hotkey " + (_registered ? "Ctrl+Alt+F registered" : "could NOT be registered"));
            if (!_registered)
                Balloon("Ctrl+Alt+F was taken", "Something else already owns it - use the tray icon menu instead.");
        }

        private void Toggle()
        {
            if (_free) { HandBack(); return; }

            _free = true;
            CursorGate.Free();
            _reclips = 0;
            _freeItem.Checked = true;
            Line("cursor freed; clip is now " + CursorGate.Current());
            Balloon("Mouse is free", "Move it to another monitor. Ctrl+Alt+F hands it back to the game.");
        }

        private void HandBack()
        {
            _free = false;
            _freeItem.Checked = false;

            CursorGate.Rect rect;
            if (CursorGate.ConfineToForegroundWindow(out rect))
                Line("handed back: cursor confined to the front window " + rect);
            else
            {
                CursorGate.Free();
                Line("handed back: no measurable front window, so the cursor stays free");
            }

            Balloon("CursorFree", "The front window has the mouse again.");
        }

        private void KeepFree()
        {
            if (!_free) return;
            if (!CursorGate.IsConfined()) return;

            // Something - almost certainly the game - put its clip back. Count it: if this climbs
            // quickly, the game is re-clipping continuously and the log will show exactly that.
            _reclips++;
            CursorGate.Free();

            if (_reclips == 1)
                Line("the clip came back and was removed again");
            else if (_reclips % 200 == 0)
                Line("still fighting the clip: " + _reclips + " removals so far");
        }

        private void Report()
        {
            var clip = CursorGate.Current();
            var desktop = CursorGate.Desktop();
            var free = !CursorGate.IsConfined();

            Line("clip " + clip + " | desktop " + desktop + " | freeing=" + _free +
                 " | removals=" + _reclips + " | cursor free=" + free);
            Balloon("CursorFree", "Clip is " + clip + " - the cursor is " + (free ? "free" : "confined"));
        }

        private void OpenLog()
        {
            try { Process.Start("notepad.exe", _log); } catch { }
        }

        private void Balloon(string title, string text)
        {
            try
            {
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = text;
                _tray.ShowBalloonTip(4000);
            }
            catch { }
        }

        private void Line(string message)
        {
            try
            {
                File.AppendAllText(_log, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { }
        }

        private static string AppFolder()
        {
            try
            {
                var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                {
                    var folder = Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) return folder;
                }
            }
            catch { }

            return Environment.CurrentDirectory;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_registered) { try { UnregisterHotKey(_window.Handle, HotkeyId); } catch { } }
                _watch.Stop();
                _tray.Visible = false;
            }

            base.Dispose(disposing);
        }
    }
}
