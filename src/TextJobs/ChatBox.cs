using System;
using System.Collections.Generic;
using System.Drawing;
using GTA.Native;
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
    ///
    /// Three things about drawing through ScriptHookVDotNet are not obvious, and every one of them has
    /// been a fault at some point. They are the reason this file looks the way it does:
    ///
    /// * **The units are not screen pixels.** ScaledDraw maps a position onto the screen by dividing it
    ///   by Screen.ScaledWidth and by Screen.Height (720) - a base 720 pixels tall, 1280 wide at 16:9,
    ///   whatever the real screen is. The layout here is in the real pixels the police box uses, so it
    ///   is converted once, on the way out. Feeding real pixels into Draw() drew the whole box a third
    ///   larger than intended on a 1080p screen and left most of it off the right edge - which is what
    ///   the picture that started this looked like.
    /// * **A long line is wrapped by the game, silently.** A TextElement that is not given a wrap width
    ///   does not set one, so the boundary is whatever the last script to draw text left behind, and
    ///   anything past it continues on a second row a font line below the first. This box now sets the
    ///   width itself and reserves the rows a line needs, so nothing can be drawn through the line
    ///   below it.
    /// * **The text size has to be measured, not assumed.** The game's font is not Arial and what a
    ///   scale value means depends on the resolution, so the size is worked out by asking the game how
    ///   wide its own text is and matching that to the police box's Arial.
    /// </summary>
    internal sealed class ChatBox
    {
        private sealed class Line
        {
            public string Text;
            public Color Colour;

            /// <summary>
            /// Rows this line takes in the panel, and the width, position and text scale that answer
            /// was worked out for - a line only changes height when the box does, so it is measured
            /// when it is first drawn rather than every frame.
            /// </summary>
            public int Rows = 1;
            public float RowsWidth = -1f;
            public float RowsX = -1f;
            public float RowsScale = -1f;
        }

        private readonly List<Line> _lines = new List<Line>();
        private readonly List<string> _history = new List<string>();
        private int _historyAt;
        private int _lastOutput;
        private bool _renderFailed;

        // Measured once per text size, then used for every line: how wide one 'M' is in screen pixels
        // at the scale that makes the game's font the size the police box draws, and that scale.
        private float _textScale = 0.24f;
        private float _pixelsPerCharacter;
        private float _measuredFor = -1f;
        private float _measuredForBase = -1f;

        // What one real screen pixel is worth in ScriptHookVDotNet's own base, in each axis, set at the
        // top of every frame. Kept apart because an aspect-ratio override in the game's settings makes
        // the two different, and a box drawn with one of them used for both would be the wrong shape.
        private float _toBaseX = 1f;
        private float _toBaseY = 1f;

        public bool IsOpen;
        public string Input = "";

        public Corner Position = Corner.TopRight;
        public float Margin = 16f;

        /// <summary>The text size, in pixels at 1080p - the same units TextDispatch.ini uses.</summary>
        public float FontSize = 15f;

        /// <summary>Everything scales from this, as TextDispatch's /ui does.</summary>
        public float UiScale = 1f;

        /// <summary>How many rows the box shows. TextDispatch's default is ten, and a line long
        /// enough to wrap takes more than one row.</summary>
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

                // Never trust the reported resolution for layout: if the game answers 0 (or throws),
                // the box would be sized to nothing and disappear - the worst way for this to fail,
                // because it looks exactly like a plugin that never loaded. TextDispatch guards the
                // same read in the same way.
                var screen = Screen.Resolution;
                if (screen.Width <= 0 || screen.Height <= 0) screen = new System.Drawing.Size(1920, 1080);

                var scale = UiScale <= 0f ? 1f : UiScale;
                var fontSize = FontSize * scale;
                MeasureUnits(screen);

                // The size wanted: how wide a capital M is in the police box's Arial at this size. 'M'
                // because it is the widest and the most consistent letter there is, and 0.83 because
                // that is an Arial M's share of its point size - so "15px" here means the same size the
                // police box draws, rather than a number that happened to look right once.
                var wantedPixelsPerCharacter = fontSize * 0.83f;
                Calibrate(wantedPixelsPerCharacter);

                var lineHeight = LineHeight(fontSize);

                // The police box's width, with a floor so a small window cannot squeeze the panel
                // narrower than the text it has to hold.
                var width = Math.Min(screen.Width * 0.46f, 820f * scale);
                if (width < 360f) width = Math.Min(screen.Width * 0.90f, 360f);
                var textWidth = width - 12f;

                // Where the text starts: the panel's left edge plus the padding. The corner decides x
                // alone, so it is known before the panel's height is - which the wrapped rows decide.
                var left = Position == Corner.TopLeft || Position == Corner.BottomLeft
                    ? Margin
                    : screen.Width - width - Margin;
                var textX = left + 2f;

                // Which entries are on screen is decided in rows, not in entries: a line long enough to
                // wrap takes two or three of them, and ten entries that each wrapped would be a panel
                // taller than the screen. Newest backwards, so what was just said is what is shown;
                // an entry that does not fit is not drawn at all, and the log has it.
                var budget = Math.Max(3, Lines);
                var first = _lines.Count;
                var used = 0;
                while (first > 0)
                {
                    var takes = RowsOf(_lines[first - 1], textX, textWidth);
                    if (used > 0 && used + takes > budget) break;
                    used += takes;
                    first--;
                    if (used >= budget) break;
                }

                // The line being typed, while the box is open, is drawn and counted like any other -
                // a caret that blinks, and the hint where the text would be.
                string typedLine = null;
                var typedColour = Typing;
                var typedRows = 0;
                if (IsOpen)
                {
                    var typed = Input ?? "";
                    var caret = typed.Length > 0 && (Environment.TickCount / 450) % 2 == 0 ? "_" : "";
                    typedLine = "> " + (typed.Length == 0 ? OpenHint : typed + caret);
                    typedColour = typed.Length == 0 ? Empty : Typing;
                    typedRows = RowsOfText(typedLine, textX, textWidth);
                }

                var rows = used + typedRows;
                if (rows <= 0) return;

                var height = rows * lineHeight + 12f;
                var top = Position == Corner.TopLeft || Position == Corner.TopRight
                    ? Margin
                    : screen.Height - height - Margin;

                // The panel sits outside the text, so the first line is not touching the edge.
                new ContainerElement(new PointF(ToBaseX(left - 8f), ToBaseY(top - 6f)),
                                     new SizeF(ToBaseX(width + 16f), ToBaseY(height + 8f)),
                                     Panel).ScaledDraw();

                // The last screenful of lines, oldest first, each moving the one below it down by the
                // rows it takes, and whatever is being typed underneath.
                var y = top;
                for (int i = first; i < _lines.Count; i++)
                {
                    var line = _lines[i];
                    DrawLine(line.Text, line.Colour, textX, y, textWidth);
                    y += RowsOf(line, textX, textWidth) * lineHeight;
                }

                if (typedLine != null) DrawLine(typedLine, typedColour, textX, y, textWidth);

                // Put the game's text wrap back where it was found. Every element above set it - that
                // is what makes the wrapping this box's decision rather than the last script's to
                // draw text - and another script drawing after this one should not inherit a chat
                // box's idea of where a line ends.
                Function.Call(Hash.SET_TEXT_WRAP, 0f, 1f);
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
        /// One line, drawn with the box's own wrap width - the width of the panel.
        ///
        /// That width is the whole point of passing it. A TextElement that is not given a wrap width
        /// does not set one, so a line longer than a row is broken wherever the last script to call
        /// SET_TEXT_WRAP left the boundary, and its continuation is drawn a font line lower. This box
        /// reserves the rows a line takes, and reserving them only means anything if the width the
        /// game breaks at is the width the count was measured against.
        /// </summary>
        private void DrawLine(string text, Color colour, float x, float y, float textWidth)
        {
            new TextElement(text, new PointF(ToBaseX(x), ToBaseY(y)), _textScale, colour,
                            GameFont.ChaletLondon, Alignment.Left, false, false, ToBaseX(textWidth))
                .ScaledDraw();
        }

        /// <summary>
        /// How many rows a line takes at the box's width, asked of a TextElement set up exactly as the
        /// one that will draw it: ScaledLineCount uses the game's own arithmetic on the same wrap
        /// width, so the count cannot disagree with what is drawn. An answer worked out any other way
        /// could, and the cost of being wrong is a line drawn through the one below it.
        /// </summary>
        private int RowsOfText(string text, float x, float textWidth)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            try
            {
                var probe = new TextElement(text, new PointF(ToBaseX(x), 0f), _textScale, Color.White,
                                            GameFont.ChaletLondon, Alignment.Left, false, false,
                                            ToBaseX(textWidth));
                return Math.Max(1, probe.ScaledLineCount);
            }
            catch
            {
                // One row is the safe answer: the line is still drawn, whole, and at worst it is drawn
                // over the line below it - which is what the box did before this existed.
                return 1;
            }
        }

        private int RowsOf(Line line, float x, float textWidth)
        {
            if (line.RowsWidth != textWidth || line.RowsX != x || line.RowsScale != _textScale)
            {
                line.Rows = RowsOfText(line.Text, x, textWidth);
                line.RowsWidth = textWidth;
                line.RowsX = x;
                line.RowsScale = _textScale;
            }

            return line.Rows;
        }

        /// <summary>
        /// The vertical distance one row gets.
        ///
        /// The police box's rule is 1.55 lines of its 15px Arial, and this starts there so the two
        /// boxes have the same air between their lines. But the game's font is a different one, its
        /// line pitch cannot be asked for, and a row that touches the one below it is the one fault
        /// that makes a chat box unreadable - so the spacing is also held above the width of an 'M',
        /// which is more than the game's font has been seen to use for a line, and the larger wins.
        /// </summary>
        private float LineHeight(float fontSize)
        {
            return Math.Max(fontSize * 1.55f, _pixelsPerCharacter * 1.9f);
        }

        /// <summary>
        /// What one real screen pixel is worth in ScriptHookVDotNet's own base, in each axis.
        ///
        /// ScaledDraw divides by Screen.ScaledWidth and Screen.Height, so its base is a screen 720
        /// pixels tall however tall the real one is - 1280x720 at 16:9, 1706x720 on a 21:9 monitor.
        /// The layout above is in real pixels, and this is the one conversion between the two; the
        /// measured text width comes back through the same conversion, which is what keeps the size
        /// of the text and the space around it in the same units.
        /// </summary>
        private void MeasureUnits(System.Drawing.Size screen)
        {
            float baseWidth = 1280f, baseHeight = 720f;
            try
            {
                var scaled = Screen.ScaledWidth;
                if (scaled > 1f) baseWidth = scaled;
            }
            catch { }

            _toBaseX = screen.Width > 0 ? baseWidth / screen.Width : 1f;
            _toBaseY = screen.Height > 0 ? baseHeight / screen.Height : 1f;
        }

        private float ToBaseX(float pixels) { return pixels * _toBaseX; }
        private float ToBaseY(float pixels) { return pixels * _toBaseY; }

        /// <summary>
        /// Work out the scale that makes the game's font the size the police box draws its Arial, by
        /// measuring it rather than trusting a conversion. Once per text size: the probe is a string of
        /// capital Ms because they are the widest and the most consistent letters there are.
        ///
        /// The measurement arrives in ScriptHookVDotNet's own pixels and the size wanted is in real
        /// ones, so one of the two is converted - leaving them in different units is what made the box
        /// a third too large on a 1080p screen while the text inside it looked about right.
        /// </summary>
        private void Calibrate(float wantedPixelsPerCharacter)
        {
            if (wantedPixelsPerCharacter <= 0f) return;

            var wantedBase = ToBaseX(wantedPixelsPerCharacter);
            if (Math.Abs(_measuredFor - wantedPixelsPerCharacter) < 0.05f &&
                Math.Abs(_measuredForBase - _toBaseX) < 0.0001f && _pixelsPerCharacter > 0f) return;

            const float probeScale = 0.34f;
            try
            {
                var probe = new TextElement("MMMMMMMMMM", new PointF(0f, 0f), probeScale,
                                            Color.White, GameFont.ChaletLondon, Alignment.Left);
                var measured = probe.ScaledWidth / 10f;

                if (measured > 0.5f)
                {
                    var factor = _toBaseX > 0f ? _toBaseX : 1f;
                    _measuredFor = wantedPixelsPerCharacter;
                    _measuredForBase = _toBaseX;

                    _textScale = probeScale * (wantedBase / measured);
                    _pixelsPerCharacter = measured * (_textScale / probeScale) / factor;

                    // A scale nobody can read or one that fills the screen is not a scale: if the
                    // measurement is somehow nonsense, use the size worked out from what this font is
                    // known to draw, rather than a scale that came out of a bad number.
                    if (_textScale < 0.10f || _textScale > 1.20f)
                    {
                        Log.Line("the measured text scale came out at " + _textScale.ToString("0.###") +
                                 " - using the default instead, and the ini has FontSize and UiScale to taste");
                        Notice("Box size: the game reported an odd text width - using the default. FontSize and UiScale in TextJobs.ini will change it.");
                        _textScale = DefaultTextScale(wantedPixelsPerCharacter);
                        _pixelsPerCharacter = wantedPixelsPerCharacter;
                    }

                    // Written down once per size, because this is the arithmetic that was wrong and a
                    // log line is what turns "the box looks wrong" into a number to change.
                    Log.Line("box: text scale " + _textScale.ToString("0.###") + ", " +
                             _pixelsPerCharacter.ToString("0.0") + "px per character, " +
                             LineHeight(wantedPixelsPerCharacter / 0.83f).ToString("0.0") + "px lines, " +
                             wantedPixelsPerCharacter.ToString("0.0") + "px of 'M' wanted, " +
                             (1f / (_toBaseX > 0f ? _toBaseX : 1f)).ToString("0.0") +
                             " screen px per SHVDN px at " + Screen.Resolution.Width + "x" + Screen.Resolution.Height);
                }
            }
            catch (Exception ex)
            {
                // Measuring is a nicety; not measuring is not worth losing the box over - but the
                // player is told, because otherwise the numbers are invisible and the box size is
                // a mystery that costs another session to work out.
                Log.Line("could not measure the game's text width: " + ex.Message);
                Notice("Box size: could not measure the game's text - using the default size.");
                _textScale = DefaultTextScale(wantedPixelsPerCharacter);
                _pixelsPerCharacter = wantedPixelsPerCharacter;
                _measuredFor = wantedPixelsPerCharacter;
                _measuredForBase = _toBaseX;
            }
        }

        /// <summary>
        /// The scale to use when the game will not say how wide its text is.
        ///
        /// Not a fixed number: the size wanted is in screen pixels and a font scale is not, so the
        /// answer moves with the resolution. GTA's HUD font draws a capital M about 34 of
        /// ScriptHookVDotNet's pixels wide per unit of scale, which is where the 34 comes from - it is
        /// the one measurement in this file that cannot be taken when measurement fails, so it is
        /// written down as what it is.
        /// </summary>
        private float DefaultTextScale(float wantedPixelsPerCharacter)
        {
            var wantedBase = ToBaseX(wantedPixelsPerCharacter);
            var scale = wantedBase / 34f;
            if (scale < 0.10f) return 0.10f;
            if (scale > 1.20f) return 1.20f;
            return scale;
        }
    }
}
