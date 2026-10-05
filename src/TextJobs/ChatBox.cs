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
    /// It is drawn with ScriptHookVDotNet's own screen elements rather than by hooking DirectX, which
    /// is the honest way to do it from a script: the game is already drawing, and this adds a panel to
    /// it. Elements are made fresh each frame, because that is what they are - a drawing instruction
    /// rather than a retained object.
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
        public float Scale = 0.34f;
        public int Lines = 12;
        public int TranscriptSeconds = 25;

        /// <summary>The key that opens the box, for the line shown while it is closed.</summary>
        public string OpenHint = "press the chat key for jobs";

        private static readonly Color Said = Color.FromArgb(240, 240, 240);
        private static readonly Color Noticed = Color.FromArgb(170, 220, 255);
        private static readonly Color Failed = Color.FromArgb(255, 150, 150);
        private static readonly Color Faded = Color.FromArgb(150, 150, 150);
        private static readonly Color Typing = Color.FromArgb(255, 255, 210);
        private static readonly Color Panel = Color.FromArgb(175, 0, 0, 0);

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

        /// <summary>True while there is something worth showing: open, or freshly said.</summary>
        private bool Visible()
        {
            if (IsOpen) return true;
            if (_lines.Count == 0) return false;
            return Environment.TickCount - _lastOutput < Math.Max(2, TranscriptSeconds) * 1000;
        }

        /// <summary>Draw. Called once a frame; does nothing when there is nothing to say.</summary>
        public void Render()
        {
            try
            {
                if (!Visible()) return;

                var screen = Screen.Resolution;
                var lineHeight = Scale * 34f;

                var width = Math.Min(760f, Math.Max(420f, screen.Width * 0.42f));
                var shown = Math.Max(4, Lines);
                var height = (shown + 2) * lineHeight + 14f;

                float left, top;
                switch (Position)
                {
                    case Corner.TopLeft: left = Margin; top = Margin; break;
                    case Corner.BottomLeft: left = Margin; top = screen.Height - height - Margin; break;
                    case Corner.BottomRight: left = screen.Width - width - Margin; top = screen.Height - height - Margin; break;
                    default: left = screen.Width - width - Margin; top = Margin; break;
                }

                new ContainerElement(new PointF(left, top), new SizeF(width, height), Panel).Draw();

                // The last screenful of lines, oldest first, and whatever is being typed underneath.
                var first = Math.Max(0, _lines.Count - shown);
                var characters = (int)Math.Max(20, (width - 20f) / (Scale * 9.2f));
                var y = top + 7f;

                for (int i = first; i < _lines.Count; i++)
                {
                    new TextElement(Clip(_lines[i].Text, characters), new PointF(left + 10f, y), Scale,
                                    _lines[i].Colour, GameFont.ChaletLondon, Alignment.Left).Draw();
                    y += lineHeight;
                }

                if (IsOpen)
                {
                    new TextElement("> " + Clip(Input, characters - 3) + "_", new PointF(left + 10f, y), Scale,
                                    Typing, GameFont.ChaletLondon, Alignment.Left).Draw();
                }
                else
                {
                    new TextElement(OpenHint, new PointF(left + 10f, y), Scale * 0.85f, Faded,
                                    GameFont.ChaletLondon, Alignment.Left).Draw();
                }
            }
            catch (Exception ex)
            {
                // This runs every frame: a failure is reported once and then left alone, or one bad
                // element would fill the log in seconds.
                if (_renderFailed) return;
                _renderFailed = true;
                Log.Error("draw", ex);
            }
        }

        private static string Clip(string text, int characters)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= characters) return text;
            return text.Substring(0, Math.Max(1, characters - 3)) + "...";
        }
    }
}
