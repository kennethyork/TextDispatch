using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TextDispatch.Chat
{
    /// <summary>
    /// The session's transcript, kept for writing out.
    ///
    /// On GTA World the roleplay outlives the session as a chat log: a screenshot, or a log pasted into a
    /// forum thread. This is the same thing - every line the box showed, with the time it was shown - so a
    /// shift can be read back, posted or kept. The box itself only holds its last 400 lines for drawing;
    /// this holds the session.
    ///
    /// Two forms. Plain text is a chat log as a player would paste it. BBCode is the same lines in the
    /// colours the box drew them, inside a spoiler, ready for a phpBB or Invision forum post.
    /// </summary>
    public static class ChatLog
    {
        private static readonly object Gate = new object();
        private static readonly List<Entry> Lines = new List<Entry>();
        private static int _exportedUpTo;

        /// <summary>A day's roleplay is a lot of lines; past this the oldest go, and the export says so.</summary>
        private const int Limit = 20000;
        private static int _dropped;

        public sealed class Entry
        {
            public DateTime At;
            public ChatChannel Channel;
            public string Tag;
            public string Text;
        }

        public static int Count { get { lock (Gate) return Lines.Count; } }

        /// <summary>Lines added since the last export.</summary>
        public static int Unexported { get { lock (Gate) return Math.Max(0, Lines.Count - _exportedUpTo); } }

        internal static void Add(ChatChannel channel, string tag, string text)
        {
            lock (Gate)
            {
                Lines.Add(new Entry { At = DateTime.Now, Channel = channel, Tag = tag, Text = text });
                if (Lines.Count > Limit)
                {
                    var excess = Lines.Count - Limit;
                    Lines.RemoveRange(0, excess);
                    _dropped += excess;
                    _exportedUpTo = Math.Max(0, _exportedUpTo - excess);
                }
            }
        }

        /// <summary>
        /// Whether a line is roleplay - something said, done or heard - as opposed to the box talking to the
        /// player about itself. A posted log is the scene, not the help text.
        /// </summary>
        public static bool IsRoleplay(ChatChannel channel)
        {
            return channel != ChatChannel.Notice && channel != ChatChannel.Error;
        }

        public static string Folder()
        {
            return Path.Combine(Settings.PluginFolder(), "TextDispatch Logs");
        }

        /// <summary>
        /// Write the log out: the roleplay only, or everything with `all`; the whole session, or only what is
        /// new since the last export with `since`. Returns the two files written, or throws.
        /// </summary>
        public static string[] Export(bool all, bool since, string unit, out int written)
        {
            List<Entry> snapshot;
            int dropped, from;
            lock (Gate)
            {
                from = since ? Math.Min(_exportedUpTo, Lines.Count) : 0;
                snapshot = Lines.GetRange(from, Lines.Count - from);
                dropped = since ? 0 : _dropped;
                _exportedUpTo = Lines.Count;
            }

            var chosen = new List<Entry>();
            foreach (var line in snapshot)
                if (all || IsRoleplay(line.Channel)) chosen.Add(line);
            written = chosen.Count;

            var folder = Folder();
            Directory.CreateDirectory(folder);
            var stamp = DateTime.Now.ToString("yyyy-MM-dd HHmm");
            var plainPath = Path.Combine(folder, stamp + " chatlog.txt");
            var bbPath = Path.Combine(folder, stamp + " chatlog (forum BBCode).txt");

            var title = "Chat log - " + (string.IsNullOrEmpty(unit) ? "on duty" : unit) + " - " +
                        (chosen.Count > 0 ? chosen[0].At.ToString("yyyy-MM-dd HH:mm") + " to " + chosen[chosen.Count - 1].At.ToString("HH:mm")
                                          : DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

            var plain = new StringBuilder();
            plain.AppendLine(title);
            if (dropped > 0) plain.AppendLine("(the first " + dropped + " lines of the session were too old to keep)");
            plain.AppendLine();
            foreach (var line in chosen) plain.AppendLine("[" + line.At.ToString("HH:mm:ss") + "] " + Line(line));

            var bb = new StringBuilder();
            bb.AppendLine("[b]" + title + "[/b]");
            bb.AppendLine("[spoiler=Chat log][font=Arial][size=85][background=#000000]");
            foreach (var line in chosen)
                bb.AppendLine("[color=" + Colour(line.Channel) + "]" + Escape(Line(line)) + "[/color]");
            bb.AppendLine("[/background][/size][/font][/spoiler]");

            File.WriteAllText(plainPath, plain.ToString(), Encoding.UTF8);
            File.WriteAllText(bbPath, bb.ToString(), Encoding.UTF8);
            return new[] { plainPath, bbPath };
        }

        private static string Line(Entry line)
        {
            return string.IsNullOrEmpty(line.Tag) ? line.Text : line.Tag + " " + line.Text;
        }

        /// <summary>The box's own colours, so a posted log reads the way the scene looked.</summary>
        private static string Colour(ChatChannel channel)
        {
            switch (channel)
            {
                case ChatChannel.Dispatch: return "#7AB0FF";
                case ChatChannel.Radio: return "#78E8EC";
                case ChatChannel.Local: return "#C6E8FF";
                case ChatChannel.Me: return "#D698FF";
                case ChatChannel.Do: return "#B0B0B0";
                case ChatChannel.Phone: return "#F5D76E";
                case ChatChannel.Notice: return "#FFD67A";
                case ChatChannel.Error: return "#FF7A7A";
                default: return "#F0F0F0";
            }
        }

        /// <summary>
        /// The box's own tags - [DISPATCH], [RADIO] - are not BBCode and forums show them as typed. Only a
        /// line that happens to contain a real tag's name is broken up, with a zero-width space.
        /// </summary>
        private static string Escape(string text)
        {
            var t = text ?? "";
            foreach (var tag in new[] { "b", "i", "u", "s", "url", "img", "color", "size", "font", "quote", "spoiler", "code", "list", "background" })
            {
                t = t.Replace("[" + tag + "]", "[​" + tag + "]").Replace("[/" + tag + "]", "[​/" + tag + "]")
                     .Replace("[" + tag + "=", "[​" + tag + "=");
            }
            return t;
        }
    }
}
