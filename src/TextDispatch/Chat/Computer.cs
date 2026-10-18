using System;
using System.Collections.Generic;

namespace TextDispatch.Chat
{
    /// <summary>
    /// The mobile data computer: a second tab in the chat box. The box shows either the conversation (CHAT)
    /// or the computer's screen (MDT) - people, vehicles, warrants, BOLOs, the dispatch screen, the units,
    /// reports, cases, flags, messages - in the same corner, in the same text.
    ///
    /// Everything on a screen is what the box's own lookups say, caught on the way to the box
    /// (ChatBox.BeginCapture) and kept here instead, so the computer and the commands can never disagree.
    /// It is driven from the box: /mdt and a tab, or - while the MDT tab is showing - a name or a plate
    /// typed on its own.
    /// </summary>
    public sealed class Computer
    {
        public static readonly string[] Tabs = { "HOME", "PERSON", "VEHICLE", "WARRANTS", "BOLO", "CAD", "UNITS", "REPORTS", "CASES", "FLAGS", "MSG" };

        private readonly object _gate = new object();
        private List<ChatLine> _lines = new List<ChatLine>();
        private string _tab = "HOME";
        private string _title = "";
        private int _page;

        /// <summary>The box this is a tab of.</summary>
        public ChatBox Box;

        /// <summary>Whether the box is showing the MDT tab. Closing it goes back to the chat.</summary>
        public bool Open
        {
            get { return Box != null && Box.Tab == ChatTab.Mdt; }
            set
            {
                if (Box == null) return;
                if (value) Box.Tab = ChatTab.Mdt;
                else if (Box.Tab == ChatTab.Mdt) Box.Tab = ChatTab.Chat;
            }
        }
        public string Unit = "";

        public string Tab { get { lock (_gate) return _tab; } }

        /// <summary>Put a screen up: the tab it belongs to, a title, and the lines it shows.</summary>
        public void Show(string tab, string title, List<ChatLine> lines)
        {
            lock (_gate)
            {
                _tab = tab;
                _title = title ?? "";
                _lines = lines ?? new List<ChatLine>();
                _page = 0;
            }
            Open = true;
        }

        public void Page(int delta)
        {
            lock (_gate) _page = Math.Max(0, _page + delta);
        }

        /// <summary>
        /// The screen as box lines, `rows` of them: the computer's own tabs, the title, a page of results and,
        /// when there is more than a page, where you are in it. The box puts its own tab strip above.
        /// </summary>
        public List<ChatLine> Screen(int rows)
        {
            string tab, title;
            List<ChatLine> lines;
            int page;
            lock (_gate)
            {
                tab = _tab;
                title = _title;
                lines = new List<ChatLine>(_lines);
                page = _page;
            }

            var strip = new System.Text.StringBuilder();
            foreach (var name in Tabs) strip.Append(name == tab ? "[" + name + "] " : name + " ");
            var screen = new List<ChatLine>
            {
                new ChatLine { Channel = ChatChannel.Dispatch, Text = Unit + " " + DateTime.Now.ToString("HH:mm") + "   " + strip.ToString().TrimEnd() },
                new ChatLine { Channel = ChatChannel.Notice, Text = "> " + title }
            };

            var room = Math.Max(3, rows - screen.Count - 1);
            var pages = Math.Max(1, (lines.Count + room - 1) / room);
            page = Math.Min(page, pages - 1);
            for (int i = page * room; i < lines.Count && i < (page + 1) * room; i++) screen.Add(Mark(lines[i]));

            screen.Add(new ChatLine
            {
                Channel = ChatChannel.Do,
                Text = (pages > 1 ? "page " + (page + 1) + "/" + pages + " - /mdt next, /mdt back.  " : "") + "Type a name or plate. /chat goes back."
            });
            return screen;
        }

        /// <summary>A result line, coloured the way the computer reads it: wanted and stolen in red, BOLOs amber.</summary>
        private static ChatLine Mark(ChatLine line)
        {
            var t = line.Text ?? "";
            var channel = line.Channel;
            if (t.Contains("WARRANT") || t.Contains("FLAG") || t.Contains("STOLEN") || t.Contains("SUSPENDED") || t.Contains("WANTED"))
                channel = ChatChannel.Error;
            else if (t.Contains("BOLO")) channel = ChatChannel.Phone;
            else if (channel == ChatChannel.Notice && t.StartsWith("  ")) channel = ChatChannel.Local;
            return new ChatLine { Channel = channel, Tag = line.Tag, Text = t };
        }
    }
}
