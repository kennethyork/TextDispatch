using System;
using System.Collections.Generic;
using System.Text;
using Rage;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace TextDispatch.Chat
{
    /// <summary>Which voice a line belongs to. Drives the colour, and nothing else.</summary>
    public enum ChatChannel
    {
        Dispatch,
        Radio,
        Local,
        Speech,
        Me,
        Do,
        Notice,
        Error,
        Phone
    }

    public sealed class ChatLine
    {
        public ChatChannel Channel;
        public string Tag;
        public string Text;

        /// <summary>Which tab it belongs to.</summary>
        public ChatTab Tab;

        /// <summary>Shown on every tab: a P1 call, a request for help, a warrant or a stolen car on your stop.</summary>
        public bool Urgent;

        /// <summary>When it was written, Environment.TickCount - so an urgent line can be shown on the MDT tab while it is fresh.</summary>
        public int At = Environment.TickCount;
    }

    /// <summary>
    /// The box's tabs. CHAT is the scene - what is said and done, and what commands answer; RADIO is
    /// dispatch, the other units, the other departments and the 911 calls; MDT is the computer; PHONE is
    /// texts and calls. ALL is everything mixed, the way the box was before it had tabs.
    /// </summary>
    public enum ChatTab
    {
        Chat,
        Radio,
        Mdt,
        Phone,
        All,
        Radar      // the radar's head unit, live
    }

    /// <summary>
    /// Which corner the box is anchored to. Configurable because LSPDFR installs are crowded: the
    /// top-left is the most contested patch of screen in the game, and a chat box that sits on top of
    /// another plugin's panel makes both useless.
    /// </summary>
    public enum ChatCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public static class ChatCorners
    {
        /// <summary>Accepts "top-right", "topright", "TR" and similar.</summary>
        public static bool TryParse(string text, out ChatCorner corner)
        {
            var t = (text ?? "").Trim().ToLowerInvariant()
                .Replace(" ", "").Replace("-", "").Replace("_", "");

            switch (t)
            {
                case "topleft": case "tl": corner = ChatCorner.TopLeft; return true;
                case "topright": case "tr": corner = ChatCorner.TopRight; return true;
                case "bottomleft": case "bl": corner = ChatCorner.BottomLeft; return true;
                case "bottomright": case "br": corner = ChatCorner.BottomRight; return true;
            }

            corner = ChatCorner.TopRight;
            return false;
        }

        public static string Describe(ChatCorner corner)
        {
            switch (corner)
            {
                case ChatCorner.TopLeft: return "top-left";
                case ChatCorner.BottomLeft: return "bottom-left";
                case ChatCorner.BottomRight: return "bottom-right";
                default: return "top-right";
            }
        }
    }

    /// <summary>
    /// The chat box. A scrolling transcript in one corner of the screen and, when open, an input
    /// line - the same shape a FiveM/GTA World chat has, because that is the interface being copied.
    /// The corner is configurable; top-right by default, since other LSPDFR plugins draw on the left.
    ///
    /// Threading: Render is called from RPH's render callback, which is not the fiber that writes to
    /// this box, so every read and write of the transcript takes the lock.
    /// </summary>
    public sealed class ChatBox
    {
        private readonly object _gate = new object();
        private readonly List<ChatLine> _lines = new List<ChatLine>();
        private readonly List<string> _history = new List<string>();
        private int _historyIndex = -1;

        public bool IsOpen;
        public string Input = "";
        public int Scroll;

        /// <summary>Default is top-right: the left side belongs to whoever else is drawing.</summary>
        public ChatCorner Position = ChatCorner.TopRight;

        /// <summary>
        /// Shown inside the box, on the input line, when nothing has been typed yet.
        ///
        /// It used to read "Press T to chat" and went on saying it for two releases after T stopped
        /// being the key - the same mistake as the on-duty notification, in the one place the player
        /// was actually looking. So it no longer names a key at all: while the box is open the key has
        /// already done its job, and the key that matters is named where it belongs, in the notification
        /// when the plugin loads.
        /// </summary>
        public string EmptyHint = "Type here. /help for the commands, Esc to close.";

        /// <summary>Distance from the screen edge, in pixels.</summary>
        public float Margin = 16f;

        // Adjustable at runtime so the box can be fixed without a rebuild: /ui, /font, /fontsize, /lines.
        public float UiScale = 1f;
        public string FontName = "Arial";
        public float FontSize = 15f;
        public int VisibleLines = 10;

        public int LineCount { get { lock (_gate) return _lines.Count; } }

        /// <summary>The computer - what the MDT tab shows.</summary>
        public Computer Computer;

        /// <summary>The tab showing. Switching to one clears its count of what is new.</summary>
        public ChatTab Tab
        {
            get { return _tab; }
            set
            {
                _tab = value;
                if (value == ChatTab.All) Array.Clear(_unread, 0, _unread.Length);
                else _unread[(int)value] = 0;
                Scroll = 0;
            }
        }

        private ChatTab _tab = ChatTab.Chat;

        // What has come in on each tab since it was last looked at.
        private readonly int[] _unread = new int[6];

        private static readonly ChatTab[] Order = { ChatTab.Chat, ChatTab.Radio, ChatTab.Radar, ChatTab.Mdt, ChatTab.Phone, ChatTab.All };

        /// <summary>The RADAR tab's screen, from the radar's last scan. Set by the plugin.</summary>
        public Func<int, List<ChatLine>> RadarScreen;

        /// <summary>The next tab along, or the one before: Ctrl+Tab and Ctrl+Shift+Tab.</summary>
        public void CycleTab(bool backwards)
        {
            var at = Array.IndexOf(Order, _tab);
            Tab = Order[(at + (backwards ? Order.Length - 1 : 1)) % Order.Length];
        }

        public int Unread(ChatTab tab) { return _unread[(int)tab]; }

        /// <summary>
        /// Which tab a line belongs to. The radio's channels go to RADIO and the phone's to PHONE; what is
        /// said and done goes to CHAT; and the box's own answers - notices, errors - go to the tab the player
        /// is on, since that is where they asked.
        /// </summary>
        private ChatTab TabOf(ChatChannel channel)
        {
            switch (channel)
            {
                case ChatChannel.Dispatch:
                case ChatChannel.Radio: return ChatTab.Radio;
                case ChatChannel.Notice:
                case ChatChannel.Error:
                    if (_tab == ChatTab.Radar) return ChatTab.Radar;
                    return _tab == ChatTab.Mdt || _tab == ChatTab.All ? ChatTab.Chat : _tab;
                case ChatChannel.Phone: return ChatTab.Phone;
                default: return ChatTab.Chat;
            }
        }

        private static readonly string[] UrgentWords =
        {
            "P1,", "P1 ", "needs assistance", "10-13", "panic", "shots fired", "officer down", "send everyone",
            "WARRANT", "STOLEN", "SUSPENDED", "be advised", "*beep*"
        };

        /// <summary>A radio line that must be seen wherever the player is looking.</summary>
        private static bool IsUrgent(ChatChannel channel, string text)
        {
            if (channel != ChatChannel.Dispatch && channel != ChatChannel.Radio) return false;
            foreach (var word in UrgentWords)
                if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ------------------------------------------------------------------ output

        private string _lastText;
        private ChatChannel _lastChannel;
        private int _lastAt;
        private int _suppressed;

        /// <summary>Lines dropped because they repeated within three seconds - for the log.</summary>
        public int SuppressedRepeats { get { return _suppressed; } }

        // While a screen of the computer is being filled, the lines go to it instead of the box.
        private List<ChatLine> _capture;

        /// <summary>Catch every line written until EndCapture, rather than showing it - for the computer.</summary>
        public void BeginCapture() { _capture = new List<ChatLine>(); }

        public List<ChatLine> EndCapture()
        {
            var caught = _capture ?? new List<ChatLine>();
            _capture = null;
            return caught;
        }

        /// <summary>
        /// The officer's name, from /officer. When it is set the box says it wherever it would say "You" about
        /// the player - "* Officer Smith checks the plate", "Officer Smith says:" - the way a roleplay server
        /// shows a character's name.
        /// </summary>
        public string PlayerName;

        private string Named(string text, bool isTag)
        {
            if (string.IsNullOrEmpty(PlayerName) || string.IsNullOrEmpty(text)) return text;
            if (isTag)
            {
                // "You say:" is "Officer Smith says:", "You whisper:" "Officer Smith whispers:".
                if (text == "You:") return PlayerName + ":";
                if (text.StartsWith("You ")) return PlayerName + " " + ThirdPerson(text.Substring(4));
                return text.Replace("] You:", "] " + PlayerName + ":");
            }
            if (text.StartsWith("> You ")) return "> " + PlayerName + " " + text.Substring(6);
            return text;
        }

        /// <summary>
        /// The first word of a phrase as he or she would say it: "check the plate" is "checks the plate",
        /// "search the car" "searches the car", "carry" "carries", and the few irregular ones.
        /// </summary>
        public static string ThirdPerson(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return phrase;

            // The verbs later in a compound action too - "kneel, check the airway and take a pulse" - but only
            // the ones the plugin's own actions use, so a noun after "and" is never turned into a verb.
            var first = FirstThird(phrase);
            foreach (var verb in Verbs)
                foreach (var joint in new[] { ", ", " and ", " then ", ", then " })
                    first = first.Replace(joint + verb + " ", joint + FirstThird(verb) + " ");
            return first;
        }

        private static readonly string[] Verbs =
        {
            "check", "take", "put", "hold", "walk", "turn", "have", "count", "call", "stand", "follow", "pack", "apply",
            "keep", "open", "hit", "sweep", "pull", "go", "start", "roll", "cool", "cover", "note", "fit", "ask", "lay",
            "search", "look", "write", "bag", "tag", "run", "give", "flip", "stop", "kneel", "listen", "radio", "wait"
        };

        private static string FirstThird(string phrase)
        {
            var space = phrase.IndexOf(' ');
            var word = space < 0 ? phrase : phrase.Substring(0, space);
            var rest = space < 0 ? "" : phrase.Substring(space);
            var tail = "";
            while (word.Length > 0 && !char.IsLetter(word[word.Length - 1])) { tail = word[word.Length - 1] + tail; word = word.Substring(0, word.Length - 1); }
            if (word.Length == 0) return phrase;

            string third;
            switch (word.ToLowerInvariant())
            {
                case "have": third = "has"; break;
                case "are": third = "is"; break;
                case "do": third = "does"; break;
                case "go": third = "goes"; break;
                default:
                    var w = word.ToLowerInvariant();
                    if (w.EndsWith("s") || w.EndsWith("sh") || w.EndsWith("ch") || w.EndsWith("x") || w.EndsWith("z")) third = word + "es";
                    else if (w.Length > 1 && w.EndsWith("y") && "aeiou".IndexOf(w[w.Length - 2]) < 0) third = word.Substring(0, word.Length - 1) + "ies";
                    else third = word + "s";
                    break;
            }
            return third + tail + rest;
        }

        public void Write(ChatChannel channel, string tag, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            tag = Named(tag, true);
            text = Named(text, false);

            if (_capture != null)
            {
                _capture.Add(new ChatLine { Channel = channel, Tag = tag, Text = Plain(text) });
                return;
            }

            // GTA's formatting tokens belong to the notification system, not to a box that draws words.
            // A callout pack that says a thing through both arrives here as
            // "~g~TextCallouts~s~: 462 callouts (18 built in, 444 from the library)" and the box drew
            // the tokens themselves, which is what was in the picture. They are taken out here, once,
            // so nothing that sends a line has to remember - and so the log shows what the player saw
            // rather than what the sender wrote.
            text = Plain(text);

            // The same line twice in a moment is never information - it is a loop somewhere upstream,
            // and a box that fills with it is unusable. Three seconds is long enough to catch the
            // repeat and short enough that a genuinely repeated radio call still gets through.
            var now = Environment.TickCount;
            if (channel == _lastChannel && text == _lastText && now - _lastAt < 3000)
            {
                _suppressed++;

                // Written down once per run, not once per occurrence: the first session this ran in
                // produced 1,871 lines of "repeat suppressed" out of 2,924, which buries everything
                // worth reading in the log it was meant to make readable.
                if (_suppressed == 1)
                    Log.Line("box: a line is repeating and is being held back - " + text);

                return;
            }

            if (_suppressed > 1)
                Log.Line("box: " + _suppressed + " repeats of the previous line were held back");
            _suppressed = 0;

            _lastText = text;
            _lastChannel = channel;
            _lastAt = now;

            // Every line that reaches the box is written down, with its channel. Without this the
            // log can only show what the plugin *decided* to say, which is exactly the half that is
            // not in question when somebody reports that the box is spamming.
            Log.Line("box " + channel + ": " + (string.IsNullOrEmpty(tag) ? "" : tag + " ") + text);

            // And kept for /log, which writes the session out as a chat log a forum post can carry.
            ChatLog.Add(channel, tag, text);

            var tab = TabOf(channel);
            // The radar's own lines go to its tab - unless the player asked for a reading on CHAT or RADIO, where
            // it is shown where they asked. Alerts are urgent, so they are on every tab anyway.
            if (tag == "[RADAR]" && _tab != ChatTab.Chat && _tab != ChatTab.Radio) tab = ChatTab.Radar;
            var urgent = IsUrgent(channel, (tag ?? "") + " " + text);
            if (_tab != ChatTab.All && tab != _tab && !(urgent && _tab != ChatTab.Mdt)) _unread[(int)tab]++;

            lock (_gate)
            {
                _lines.Add(new ChatLine { Channel = channel, Tag = tag, Text = text, Tab = tab, Urgent = urgent });
                if (_lines.Count > 400) _lines.RemoveRange(0, _lines.Count - 400);
            }
            if (Scroll > 0) Scroll++;
        }

        public void Dispatch(string text) { Write(ChatChannel.Dispatch, "[DISPATCH]", text); }
        public void Radio(string text) { Write(ChatChannel.Radio, "[RADIO]", text); }
        public void Notice(string text) { Write(ChatChannel.Notice, "", text); }
        public void Error(string text) { Write(ChatChannel.Error, "", text); }
        public void Local(string tag, string text) { Write(ChatChannel.Local, tag, text); }
        /// <summary>
        /// An action the plugin describes, in the base form: Me("You", "check the plate"). With a name set it
        /// is "* Officer Smith checks the plate".
        /// </summary>
        public void Me(string who, string text)
        {
            if (who == "You" && !string.IsNullOrEmpty(PlayerName)) Write(ChatChannel.Me, "", "* " + PlayerName + " " + ThirdPerson(text));
            else Write(ChatChannel.Me, "", "* " + who + " " + text);
        }

        /// <summary>An action the player typed with /me, as they wrote it.</summary>
        public void MeTyped(string text)
        {
            Write(ChatChannel.Me, "", "* " + (string.IsNullOrEmpty(PlayerName) ? "You" : PlayerName) + " " + text);
        }
        public void Do(string text) { Write(ChatChannel.Do, "", "(( " + text + " ))"); }
        public void Phone(string tag, string text) { Write(ChatChannel.Phone, tag, text); }

        /// <summary>
        /// A line with GTA's ~tokens~ taken out of it.
        ///
        /// One or two letters between tildes is a colour or an effect - ~g~, ~s~, ~y~, ~r~ - and the
        /// game's own text renderer is the only thing that understands them. A tilde that is not part
        /// of a token (a path, a price, an approximation) is left alone, and ~n~, which is a line break
        /// where this draws, becomes a space: the box does its own wrapping and counts the rows it
        /// makes, so a line break hidden inside the text is a row it did not count.
        /// </summary>
        private static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('~') < 0) return text;

            var clean = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '~') { clean.Append(text[i]); continue; }

                var end = text.IndexOf('~', i + 1);
                var length = end - i - 1;
                if (end < 0 || length < 1 || length > 2)
                {
                    clean.Append(text[i]);   // a tilde that is not a token is just a tilde
                    continue;
                }

                var code = text.Substring(i + 1, length);
                if (code.Equals("n", StringComparison.OrdinalIgnoreCase)) clean.Append(' ');
                i = end;
            }

            return clean.ToString();
        }

        public void Clear()
        {
            lock (_gate) _lines.Clear();
            Scroll = 0;
        }

        // ------------------------------------------------------------------ history

        public void Remember(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lock (_gate)
            {
                if (_history.Count == 0 || _history[_history.Count - 1] != text) _history.Add(text);
                if (_history.Count > 100) _history.RemoveAt(0);
            }
            _historyIndex = -1;
        }

        public void HistoryPrev()
        {
            lock (_gate)
            {
                if (_history.Count == 0) return;
                if (_historyIndex < 0) _historyIndex = _history.Count;
                if (_historyIndex > 0) _historyIndex--;
                Input = _history[_historyIndex];
            }
        }

        public void HistoryNext()
        {
            lock (_gate)
            {
                if (_history.Count == 0 || _historyIndex < 0) return;
                if (_historyIndex < _history.Count - 1) { _historyIndex++; Input = _history[_historyIndex]; }
                else { _historyIndex = -1; Input = ""; }
            }
        }

        // ------------------------------------------------------------------ render

        /// <summary>"CHAT  RADIO (3)  [MDT]  PHONE (1)  ALL   Ctrl+Tab" - the tabs, the one showing in brackets.</summary>
        private string Strip(ChatTab current)
        {
            var text = new StringBuilder();
            foreach (var tab in Order)
            {
                var name = tab.ToString().ToUpperInvariant();
                var count = tab == current || tab == ChatTab.All ? 0 : _unread[(int)tab];
                text.Append(tab == current ? "[" + name + "]" : name).Append(count > 0 ? " (" + count + ")" : "").Append("  ");
            }
            return text.Append(" Ctrl+Tab").ToString();
        }

        private static Color ColourOf(ChatChannel channel)
        {
            switch (channel)
            {
                case ChatChannel.Dispatch: return Color.FromArgb(255, 122, 176, 255);
                case ChatChannel.Radio: return Color.FromArgb(255, 120, 232, 236);
                case ChatChannel.Local: return Color.FromArgb(255, 198, 232, 255);
                case ChatChannel.Speech: return Color.FromArgb(255, 240, 240, 240);
                case ChatChannel.Me: return Color.FromArgb(255, 214, 152, 255);
                case ChatChannel.Do: return Color.FromArgb(255, 176, 176, 176);
                case ChatChannel.Notice: return Color.FromArgb(255, 255, 214, 122);
                case ChatChannel.Error: return Color.FromArgb(255, 255, 122, 122);
                case ChatChannel.Phone: return Color.FromArgb(255, 245, 215, 110);
                default: return Color.FromArgb(255, 236, 236, 236);
            }
        }

        /// <summary>
        /// Draw one frame. Called from Game.FrameRender, where managed drawing is allowed.
        /// Never throws into the render callback.
        /// </summary>
        public void Render(Graphics graphics)
        {
            ChatLine[] snapshot;
            bool open;
            int scroll;
            int visible = Math.Max(3, VisibleLines);
            var tab = _tab;
            var computer = tab == ChatTab.Mdt && Computer != null;
            var radar = tab == ChatTab.Radar && RadarScreen != null;
            lock (_gate)
            {
                if (_lines.Count == 0 && !IsOpen && !computer && !radar) return;
                snapshot = _lines.ToArray();
                open = IsOpen;
                scroll = Scroll;
            }

            // The strip of tabs, the current one in brackets and the others with what is new on them. Shown
            // while the box is open, and while it is closed when something is waiting on another tab.
            var strip = Strip(tab);
            var waiting = false;
            for (var i = 0; i < _unread.Length; i++) if (i != (int)tab && _unread[i] > 0) waiting = true;

            if (radar)
            {
                // The RADAR tab: the head unit, live, and the radar's own lines under it.
                visible = Math.Max(visible, 12);
                var screen = RadarScreen(visible - 1);
                screen.Insert(0, new ChatLine { Channel = ChatChannel.Radio, Text = strip });
                for (var i = snapshot.Length - 1; i >= 0 && i >= snapshot.Length - 40; i--)
                {
                    var line = snapshot[i];
                    if (!line.Urgent || line.Tab == ChatTab.Radar || Environment.TickCount - line.At > 30000) continue;
                    screen.Add(new ChatLine { Channel = ChatChannel.Error, Tag = line.Tag, Text = "!! " + line.Text });
                    break;
                }
                // Too long: the oldest alert under the head unit gives way, never the readings themselves.
                while (screen.Count > visible && screen.Count > 8) screen.RemoveAt(screen.Count - 2);
                snapshot = screen.ToArray();
                scroll = 0;
            }
            else if (computer)
            {
                // The MDT tab: the computer's screen in place of the chat, a few rows taller so a record fits.
                visible = Math.Max(visible, 16);
                var screen = Computer.Screen(visible - 1);
                screen.Insert(0, new ChatLine { Channel = ChatChannel.Radio, Text = strip });

                // Something urgent in the last half minute is shown over the computer's last row, so a P1 or
                // a call for help is seen on this tab too.
                for (var i = snapshot.Length - 1; i >= 0 && i >= snapshot.Length - 40; i--)
                {
                    var line = snapshot[i];
                    if (!line.Urgent || Environment.TickCount - line.At > 30000) continue;
                    screen[screen.Count - 1] = new ChatLine { Channel = ChatChannel.Error, Tag = line.Tag, Text = "!! " + line.Text };
                    break;
                }
                snapshot = screen.ToArray();
                scroll = 0;
            }
            else
            {
                // This tab's lines, and the urgent ones from every tab.
                var onTab = new List<ChatLine>(snapshot.Length);
                foreach (var line in snapshot)
                    if (tab == ChatTab.All || line.Tab == tab || line.Urgent) onTab.Add(line);
                if (open || waiting)
                {
                    // The strip sits on top: it takes one of the rows, from the oldest end.
                    var first0 = Math.Max(0, onTab.Count - (visible - 1) - scroll);
                    var take = Math.Min(visible - 1, onTab.Count - first0);
                    var withStrip = new List<ChatLine> { new ChatLine { Channel = ChatChannel.Do, Text = strip } };
                    withStrip.AddRange(onTab.GetRange(first0, Math.Max(0, take)));
                    snapshot = withStrip.ToArray();
                    scroll = 0;
                }
                else snapshot = onTab.ToArray();
                if (snapshot.Length == 0 && !open) return;
            }

            // Never trust the reported resolution for layout: if the game answers 0 (or throws),
            // the box would be sized to nothing and disappear silently - the worst possible way for
            // this to fail, because it looks identical to "the plugin did not load".
            float screenWidth = 1920f, screenHeight = 1080f;
            try
            {
                var resolution = Game.Resolution;
                if (resolution.Width > 0f && resolution.Height > 0f)
                {
                    screenWidth = resolution.Width;
                    screenHeight = resolution.Height;
                }
            }
            catch { }

            float scale = UiScale <= 0f ? 1f : UiScale;
            float fontSize = FontSize * scale;
            float lineHeight = fontSize * 1.55f;

            int first = Math.Max(0, snapshot.Length - visible - scroll);
            int count = Math.Min(visible, snapshot.Length - first);
            int rows = open ? Math.Max(count + 1, 1) : count;
            if (rows <= 0) return;

            float width = Math.Min(screenWidth * 0.46f, 820f * scale);
            float height = rows * lineHeight + 12f;

            float x, y;
            switch (Position)
            {
                case ChatCorner.TopRight:
                    x = screenWidth - width - Margin;
                    y = Margin;
                    break;
                case ChatCorner.BottomLeft:
                    x = Margin;
                    y = screenHeight - height - Margin;
                    break;
                case ChatCorner.BottomRight:
                    x = screenWidth - width - Margin;
                    y = screenHeight - height - Margin;
                    break;
                default:
                    x = Margin;
                    y = Margin;
                    break;
            }

            graphics.DrawRectangle(new RectangleF(x - 8f, y - 6f, width + 16f, height + 8f),
                Color.FromArgb(128, 0, 0, 0));

            // Clip each line to the box. On the right edge an overlong line would otherwise run off
            // the screen instead of being cut off at the panel.
            float lineY = y;
            for (int i = first; i < first + count; i++)
            {
                var line = snapshot[i];
                var text = string.IsNullOrEmpty(line.Tag) ? line.Text : line.Tag + " " + line.Text;
                var clip = new RectangleF(x - 4f, lineY - 3f, width + 8f, lineHeight + 6f);

                graphics.DrawText(text, FontName, fontSize, new PointF(x, lineY), ColourOf(line.Channel), clip);
                lineY += lineHeight;
            }

            if (!open) return;

            string typed = Input ?? "";
            string shown = typed.Length == 0 ? EmptyHint : typed;
            var shownColour = typed.Length == 0
                ? Color.FromArgb(160, 200, 200, 200)
                : Color.FromArgb(255, 255, 255, 255);

            graphics.DrawText("> " + shown, FontName, fontSize, new PointF(x, lineY), shownColour);

            // A caret, so it is obvious the box is waiting for input.
            if ((Environment.TickCount / 450) % 2 == 0)
            {
                float caretX = x + 18f;
                try
                {
                    var size = Graphics.MeasureText("> " + typed, FontName, fontSize);
                    caretX = x + 18f + size.Width;
                }
                catch { }
                graphics.DrawRectangle(new RectangleF(caretX, lineY, 9f, fontSize),
                    Color.FromArgb(210, 255, 255, 255));
            }
        }
    }
}
