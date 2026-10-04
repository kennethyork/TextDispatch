using System;
using System.Windows.Forms;

namespace CursorFree
{
    /// <summary>
    /// GTA V has no setting for this, so it is done from outside the game.
    ///
    /// In borderless windowed mode the game confines the mouse to its own window while it has focus,
    /// using the Win32 call ClipCursor. On a multi-monitor desktop that means the mouse cannot reach
    /// any other screen. The clip is a desktop-wide setting rather than a per-process one, so this
    /// program can remove it - and hand it back when you want to play again.
    ///
    /// Left running, it sits in the tray. Ctrl+Alt+F frees the mouse; the same keys hand it back.
    /// </summary>
    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("--status", StringComparison.OrdinalIgnoreCase))
            {
                // A tray program has no console of its own, so it borrows the one that called it -
                // otherwise --status prints into nothing and looks broken.
                AttachConsole(-1);

                Console.WriteLine("clip    : " + CursorGate.Current());
                Console.WriteLine("desktop : " + CursorGate.Desktop());
                Console.WriteLine("confined: " + CursorGate.IsConfined());
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
    }
}
