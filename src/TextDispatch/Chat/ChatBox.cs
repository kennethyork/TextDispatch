using System;
using System.Collections.Generic;
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
        Error
    }

    public sealed class ChatLine
    {
        public ChatChannel Channel;
        public string Tag;
        public string Text;
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

        /// <summary>Distance from the screen edge, in pixels.</summary>
        public float Margin = 16f;

        // Adjustable at runtime so the box can be fixed without a rebuild: /ui, /font, /fontsize, /lines.
        public float UiScale = 1f;
        public string FontName = "Arial";
        public float FontSize = 15f;
        public int VisibleLines = 10;

        public int LineCount { get { lock (_gate) return _lines.Count; } }

        // ------------------------------------------------------------------ output

        public void Write(ChatChannel channel, string tag, string text)
        {
            if (text == null) return;
            lock (_gate)
            {
                _lines.Add(new ChatLine { Channel = channel, Tag = tag, Text = text });
                if (_lines.Count > 400) _lines.RemoveRange(0, _lines.Count - 400);
            }
            if (Scroll > 0) Scroll++;
        }

        public void Dispatch(string text) { Write(ChatChannel.Dispatch, "[DISPATCH]", text); }
        public void Radio(string text) { Write(ChatChannel.Radio, "[RADIO]", text); }
        public void Notice(string text) { Write(ChatChannel.Notice, "", text); }
        public void Error(string text) { Write(ChatChannel.Error, "", text); }
        public void Local(string tag, string text) { Write(ChatChannel.Local, tag, text); }
        public void Me(string who, string text) { Write(ChatChannel.Me, "", "* " + who + " " + text); }
        public void Do(string text) { Write(ChatChannel.Do, "", "(( " + text + " ))"); }

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
            lock (_gate)
            {
                if (_lines.Count == 0 && !IsOpen) return;
                snapshot = _lines.ToArray();
                open = IsOpen;
                scroll = Scroll;
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
            int visible = Math.Max(3, VisibleLines);

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
            string shown = typed.Length == 0 ? "Press T to chat. /help for commands." : typed;
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
