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

        // Measured once per text size, then used for every line: how wide one point of the game's font
        // is at the scale that makes it the size the police box draws, and that scale.
        private float _textScale = 0.34f;
        private float _pixelsPerCharacter;
        private float _measuredFor = -1f;

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

                // The police box lays itself out in pixels: 15px text, 1.55 line spacing, a panel 820
                // wide at the most. This box does the same, in the same pixels - but it cannot use
                // 15px directly, because a ScriptHookVDotNet script sizes text as a *scale*, and what a
                // scale means depends on the game's resolution. So the size is measured instead of
                // assumed: the game's font is drawn at a probe scale, the width of a known string is
                // read back, and the scale that gives the police box's pixels per character is worked
                // out from that. The last version assumed a conversion, and a player whose game was not
                // at 1080p got a box with the lines on top of each other and the text running off the
                // panel - which is what it looked like, and what this fixes.
                var wantedPixelsPerCharacter = FontSize * scale * 0.58f;
                Calibrate(wantedPixelsPerCharacter);

                // Line spacing in the police box is 1.55 lines of Arial 15px, and an Arial character
                // at 15px is about 7px wide - so its line height is 3.3 times its narrowest character.
                // Using the same ratio here means the two boxes have the same air between the lines,
                // whatever the game's font turns out to measure at. Deliberately generous: a collision
                // of two lines is the one thing that makes a chat box unreadable, and air that is
                // slightly too free is not a fault.
                var lineHeight = lineHeightFor(_pixelsPerCharacter);

                var shown = Math.Max(3, Lines);
                var count = Math.Min(shown, _lines.Count);
                var rows = IsOpen ? count + 1 : count;
                if (rows <= 0) return;

                // The police box's width, with a floor so a small window cannot squeeze the panel
                // narrower than the text it has to hold.
                var width = Math.Min(screen.Width * 0.46f, 820f * scale);
                if (width < 360f) width = Math.Min(screen.Width * 0.90f, 360f);
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
                var characters = (int)Math.Max(12, (width - 12f) / Math.Max(1f, _pixelsPerCharacter));
                var y = top;

                for (int i = first; i < _lines.Count; i++)
                {
                    new TextElement(Clip(_lines[i].Text, characters), new PointF(left + 2f, y), _textScale,
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

                new TextElement(shownText, new PointF(left + 2f, y), _textScale,
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

        /// <summary>
        /// Work out the scale that makes the game's font the size the police box draws its Arial, by
        /// measuring it rather than trusting a conversion. Once per text size: the probe is a string of
        /// capital Ms because they are the widest and the most consistent letters there are.
        /// </summary>
        private void Calibrate(float wantedPixelsPerCharacter)
        {
            if (wantedPixelsPerCharacter <= 0f) return;
            if (Math.Abs(_measuredFor - wantedPixelsPerCharacter) < 0.05f && _pixelsPerCharacter > 0f) return;

            const float probeScale = 0.34f;
            try
            {
                var probe = new TextElement("MMMMMMMMMM", new PointF(0f, 0f), probeScale,
                                            System.Drawing.Color.White, GameFont.ChaletLondon, Alignment.Left);
                var measured = probe.ScaledWidth / 10f;

                if (measured > 0.5f)
                {
                    _textScale = probeScale * (wantedPixelsPerCharacter / measured);

                    // A scale nobody can read or one that fills the screen is not a scale: if the
                    // measurement is somehow nonsense, keep the size this plugin has always used.
                    if (_textScale < 0.10f || _textScale > 1.20f)
                    {
                        Log.Line("the measured text scale came out at " + _textScale.ToString("0.###") +
                                 " - using 0.34 instead, and the ini has FontSize and UiScale to taste");
                        _textScale = 0.34f;
                        measured = wantedPixelsPerCharacter;
                    }

                    _pixelsPerCharacter = measured * (_textScale / probeScale);
                    _measuredFor = wantedPixelsPerCharacter;

                    // Written down once per size, because this is the arithmetic that was wrong and a
                    // log line is what turns "the box looks wrong" into a number to change.
                    Log.Line("box: text scale " + _textScale.ToString("0.###") + ", " +
                             _pixelsPerCharacter.ToString("0.0") + "px per character, " +
                             (lineHeightFor(_pixelsPerCharacter)).ToString("0.0") + "px lines, " +
                             (FontSize * (UiScale <= 0f ? 1f : UiScale)).ToString("0.0") + "px text wanted");
                }
            }
            catch (Exception ex)
            {
                // Measuring is a nicety; not measuring is not worth losing the box over.
                Log.Line("could not measure the game's text width: " + ex.Message);
                _textScale = 0.34f;
                _pixelsPerCharacter = wantedPixelsPerCharacter;
                _measuredFor = wantedPixelsPerCharacter;
            }
        }

        /// <summary>The line spacing for a measured character width - one place, so the log line and
        /// the drawing cannot disagree about it.</summary>
        private static float lineHeightFor(float pixelsPerCharacter)
        {
            return Math.Max(14f, pixelsPerCharacter * 3.2f);
        }

        private static string Clip(string text, int characters)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= characters ? text : text.Substring(0, Math.Max(1, characters - 1)) + ".";
        }
    }
}
