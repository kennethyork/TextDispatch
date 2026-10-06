using System;
using System.Collections.Generic;
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
    ///
    /// It checks that it can actually write before it settles on a file. That is not belt and braces:
    /// a session on 6 October loaded this script, drew its box, and left no log anywhere, because the
    /// path it had settled on could not be written and every failure was swallowed. A diagnostic that
    /// fails silently is worse than none, because it is indistinguishable from a script that never ran.
    /// So the candidates are tried in order, each has to pass a real write, and the last resort - the
    /// user's temporary folder - is somewhere a script can always write.
    /// </summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static string _path;
        private static bool _resolved;

        /// <summary>Where the log actually went. Never null once anything has been logged.</summary>
        public static string Path
        {
            get
            {
                lock (Gate)
                {
                    Resolve();
                    return _path;
                }
            }
        }

        public static void Line(string message)
        {
            try
            {
                lock (Gate)
                {
                    Resolve();
                    File.AppendAllText(_path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void Error(string where, Exception ex)
        {
            Line("ERROR " + where + ": " + ex.GetType().Name + ": " + ex.Message);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            foreach (var candidate in Candidates())
            {
                if (!CanWrite(candidate)) continue;
                _path = candidate;
                return;
            }

            // Nothing worked, which in practice means a machine that will not let this script write
            // anywhere it can see. The temp folder is the last thing to try and is named regardless.
            try { _path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TextJobs.log"); }
            catch { _path = "TextJobs.log"; }
        }

        /// <summary>Where to try, best first: beside the script, then the game, then the temp folder.</summary>
        private static IEnumerable<string> Candidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();

            Action<string> add = delegate (string candidate)
            {
                if (string.IsNullOrEmpty(candidate)) return;
                if (seen.Add(candidate)) list.Add(candidate);
            };

            try { add(System.IO.Path.Combine(TextJobs.Settings.Folder(), "TextJobs.log")); } catch { }

            var game = TextJobs.Settings.GameFolder();
            if (game != null)
            {
                add(System.IO.Path.Combine(game, "scripts", "TextJobs.log"));
                add(System.IO.Path.Combine(game, "TextJobs.log"));
            }

            try { add(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TextJobs.log")); } catch { }

            return list;
        }

        /// <summary>An empty append, which is the only honest way to know a file can be written.</summary>
        private static bool CanWrite(string candidate)
        {
            try
            {
                var folder = System.IO.Path.GetDirectoryName(candidate);
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return false;

                File.AppendAllText(candidate, "");
                return true;
            }
            catch { return false; }
        }
    }
}
