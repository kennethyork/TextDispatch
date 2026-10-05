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

                try
                {
                    var beside = System.IO.Path.GetDirectoryName(typeof(Log).Assembly.Location);
                    if (!string.IsNullOrEmpty(beside) && Directory.Exists(beside))
                    {
                        _path = System.IO.Path.Combine(beside, "TextJobs.log");
                        return _path;
                    }
                }
                catch { }

                _path = "TextJobs.log";
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
