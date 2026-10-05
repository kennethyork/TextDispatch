using System;
using System.IO;

namespace TextDispatch
{
    /// <summary>
    /// The log the shared files write to.
    ///
    /// TextJobs compiles TextDispatch's chat-box keyboard handling and its DriverJobs reader from the
    /// same source files, and those files log through this type - which is why it lives here, under
    /// TextDispatch's namespace, in a plugin that is not TextDispatch. The name on the file is
    /// TextJobs' own, so whoever reads `scripts\` can see which script wrote what.
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

                // One answer for both files: TextJobs' own Settings class already works out where the
                // script lives, and an assembly loaded from memory reports no location of its own.
                // Qualified because this type lives in TextDispatch's namespace, where "Settings"
                // would mean TextDispatch's settings class - which is not compiled into this script.
                _path = System.IO.Path.Combine(TextJobs.Settings.Folder(), "TextJobs.log");
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
            Line("ERROR " + where + ": " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
