using System;
using System.IO;

namespace TextDispatch
{
    /// <summary>
    /// A file log next to the user's data. Screen notifications vanish and disappear on a crash;
    /// this does not, and when something fails before anything is drawn it is the only evidence.
    /// </summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static string _path;

        public static string Path
        {
            get
            {
                if (_path != null) return _path;
                try
                {
                    var dir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TextDispatch");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _path = System.IO.Path.Combine(dir, "textdispatch.log");
                }
                catch { _path = "textdispatch.log"; }
                return _path;
            }
        }

        public static void Line(string message)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(Path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void Error(string where, Exception ex)
        {
            try
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                Line(where + " FAILED: " + inner.GetType().Name + ": " + inner.Message);
            }
            catch { }
        }
    }
}
