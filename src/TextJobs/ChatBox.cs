using System;
using System.Collections.Generic;
using System.Drawing;
using GTA.UI;
using TextDispatch;

// Both System.Drawing and GTA.UI have a Font, and the one wanted here is the game's.
using GameFont = GTA.UI.Font;

namespace TextJobs
{
    internal enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// The box itself: a transcript, a line you are typing, and the drawing.
    ///
    /// Drawn with ScriptHookVDotNet's own screen elements rather than by hooking DirectX, which is the
    /// honest way to do it from a script: the game is already drawing, and this adds a panel to it.
    /// Elements are made fresh each frame, because that is what they are - a drawing instruction rather
    /// than a retained object.
    ///
    /// The look is TextDispatch's box, deliberately and to the pixel: the same half-transparent panel
    /// at the same padding, the same 15px text with the same 1.55 line spacing, the same width and the
    /// same ten visible lines, the same colours per kind of line, and the same transcript that stays on
    /// screen when the box is closed instead of fading. Two chat boxes in one game should be the same
    /// box twice, and the police one came first - it is the one the player already knows.
    ///
    /// One thing cannot be copied and is worth saying: TextDispatch draws Arial, because an LSPDFR
    /// plugin can draw through RAGE Plugin Hook's own frame render. A ScriptHookVDotNet script cannot -
    /// the game will only render its own fonts - so this uses the game's, sized in pixels to match.
    /// Same size, same spacing, same panel; a slightly different typeface, and that is the floor.
    /// </summary>
    internal sealed class ChatBox
    {
        private sealed class Line
        {
            public string Text;
            public Color Colour;
        }

        private readonly List<Line> _lines = new List<Line>();
        private readonly List<string> _history = new List<string>();
        private int _historyAt;
        private int _lastOutput;
        private bool _renderFailed;

        public bool IsOpen;
        public string Input = "";

        public Corner Position = Corner.TopRight;
        public float Margin = 16f;

        /// <summary>The text size, in pixels at 1080p - the same units TextDispatch.ini uses.</summary>
        public float FontSize = 15f;

        /// <summary>Everything scales from this, as TextDispatch's /ui does.</summary>
        public float UiScale = 1f;

        /// <summary>How many lines the box shows. TextDispatch's default is ten.</summary>
        public int Lines = 10;

        /// <summary>Seconds the transcript stays after the box closes. Zero, the default, means it
        /// stays: the police box does not fade and this is meant to be the same box.</summary>
        public int TranscriptSeconds;

        /// <summary>Shown inside the open box when nothing has been typed yet.</summary>
        public string OpenHint = "Type a job name or /jobs. /help lists what else works.";

        // TextDispatch's palette, colour for colour: a line here and a line there should not be two
        // different shades of the same idea.
        private static readonly Color Said = Color.FromArgb(255, 240, 240, 240);
        private static readonly Color Noticed = Color.FromArgb(255, 255, 214, 122);
        private static readonly Color Failed = Color.FromArgb(255, 255, 122, 122);
        private static readonly Color Typing = Color.FromArgb(255, 255, 255, 255);
        private static readonly Color Empty = Color.FromArgb(160, 200, 200, 200);
        private static readonly Color Panel = Color.FromArgb(128, 0, 0, 0);

        public void Say(string text) { Add(text, Said); }
        public void Notice(string text) { Add(text, Noticed); }
        public void Error(string text) { Add(text, Failed); }

        public void Clear()
        {
            _lines.Clear();
            _lastOutput = Environment.TickCount;
        }

        private void Add(string text, Color colour)
        {
            if (string.IsNullOrEmpty(text)) return;

            _lines.Add(new Line { Text = text, Colour = colour });
            _lastOutput = Environment.TickCount;

            // A transcript nobody can read is not a transcript: the box shows a screenful, and what
            // scrolls off is in the log.
            var most = Math.Max(Lines, 8) * 4;
            while (_lines.Count > most) _lines.RemoveAt(0);
        }

        public void Remember(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _history.Add(text);
            _historyAt = _history.Count;
        }

        public void HistoryPrev()
        {
            if (_history.Count == 0) return;
            if (_historyAt > 0) _historyAt--;
            Input = _history[_historyAt];
        }

        public void HistoryNext()
        {
            if (_history.Count == 0) return;
            if (_historyAt < _history.Count - 1) { _historyAt++; Input = _history[_historyAt]; }
            else { _historyAt = _history.Count; Input = ""; }
        }

        /// <summary>True while there is something worth showing. The transcript stays: the police box
        /// keeps its lines on screen and this is meant to be the same box. Set TranscriptSeconds to a
        /// number in the ini for the older fading behaviour.</summary>
        private bool Visible()
        {
            if (IsOpen) return true;
            if (_lines.Count == 0) return false;
            if (TranscriptSeconds <= 0) return true;
            return Environment.TickCount - _lastOutput < TranscriptSeconds * 1000;
        }

        /// <summary>Draw. Called once a frame; does nothing when there is nothing to say.</summary>
        public void Render()
        {
            try
            {
                if (!Visible()) return;

                var screen = Screen.Resolution;
                var scale = UiScale <= 0f ? 1f : UiScale;

                // The police box lays itself out in pixels: 15px Arial, 1.55 line spacing, a panel
                // 820 wide at the most. The game's font is sized relative to the screen instead, so the
                // pixels are converted here against 1080p, which is the reference the police numbers
                // were chosen at. That way both boxes are the same size on a 1080p screen and both grow
                // together on a bigger one.
                var pixelsToScale = screen.Height > 0f ? (screen.Height / 1080f) * 45f : 45f;
                var fontSize = FontSize * scale;
                var textScale = fontSize / pixelsToScale;
                var lineHeight = fontSize * 1.55f;

                var shown = Math.Max(3, Lines);
                var count = Math.Min(shown, _lines.Count);
                var rows = IsOpen ? count + 1 : count;
                if (rows <= 0) return;

                var width = Math.Min(screen.Width * 0.46f, 820f * scale);
                var height = rows * lineHeight + 12f;

                float left, top;
                switch (Position)
                {
                    case Corner.TopLeft: left = Margin; top = Margin; break;
                    case Corner.BottomLeft: left = Margin; top = screen.Height - height - Margin; break;
                    case Corner.BottomRight: left = screen.Width - width - Margin; top = screen.Height - height - Margin; break;
                    default: left = screen.Width - width - Margin; top = Margin; break;
                }

                // The panel sits outside the text, so the first line is not touching the edge.
                new ContainerElement(new PointF(left - 8f, top - 6f), new SizeF(width + 16f, height + 8f), Panel).Draw();

                // The last screenful of lines, oldest first, and whatever is being typed underneath.
                var first = Math.Max(0, _lines.Count - shown);
                var characters = (int)Math.Max(20, (width - 12f) / (fontSize * 0.58f));
                var y = top;

                for (int i = first; i < _lines.Count; i++)
                {
                    new TextElement(Clip(_lines[i].Text, characters), new PointF(left + 2f, y), textScale,
                                    _lines[i].Colour, GameFont.ChaletLondon, Alignment.Left).Draw();
                    y += lineHeight;
                }

                if (!IsOpen) return;

                // Open: the line being typed, or what to type, and a caret that blinks so it is
                // obvious the box is waiting for somebody.
                var typed = Input ?? "";
                var caret = (Environment.TickCount / 450) % 2 == 0 ? "_" : "";
                var shownText = typed.Length == 0
                    ? "> " + Clip(OpenHint, characters - 3)
                    : "> " + Clip(typed, characters - 3) + caret;

                new TextElement(shownText, new PointF(left + 2f, y), textScale,
                                typed.Length == 0 ? Empty : Typing, GameFont.ChaletLondon, Alignment.Left).Draw();
            }
            catch (Exception ex)
            {
                // Never throw into a render loop: log once, then stop trying, because a box that
                // disappears with an exception every frame is worse than no box at all.
                if (_renderFailed) return;
                _renderFailed = true;
                Log.Line("could not draw the chat box: " + ex.Message);
            }
        }

        private static string Clip(string text, int characters)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= characters ? text : text.Substring(0, Math.Max(1, characters - 1)) + ".";
        }
    }
}
