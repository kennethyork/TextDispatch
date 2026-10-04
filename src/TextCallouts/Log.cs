using System;
using System.IO;
using System.Reflection;

namespace TextCallouts
{
    /// <summary>
    /// A log beside the plugin, because a callout that fails to spawn leaves no other trace: the
    /// notification simply never appears, and from inside the game that is indistinguishable from
    /// dispatch not calling you.
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
                    var folder = PluginFolder();
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        _path = System.IO.Path.Combine(folder, "textcallouts.log");
                        return _path;
                    }
                }
                catch { }

                try
                {
                    var dir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TextCallouts");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _path = System.IO.Path.Combine(dir, "textcallouts.log");
                }
                catch { _path = "textcallouts.log"; }

                return _path;
            }
        }

        /// <summary>
        /// Where the DLL is. LSPDFR loads plugins from memory, so Assembly.Location is empty for them
        /// and the folder has to be found the way LSPDFR refers to it.
        /// </summary>
        public static string PluginFolder()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                {
                    var directory = System.IO.Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) return directory;
                }
            }
            catch { }

            foreach (var root in new[] { AppDomain.CurrentDomain.BaseDirectory, Environment.CurrentDirectory })
            {
                if (string.IsNullOrEmpty(root)) continue;
                try
                {
                    var besideTheGame = System.IO.Path.Combine(root, "Plugins", "LSPDFR");
                    if (Directory.Exists(besideTheGame)) return besideTheGame;
                }
                catch { }
            }

            return Environment.CurrentDirectory;
        }

        public static void Line(string message)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message +
                                             Environment.NewLine);
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
