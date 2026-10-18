using System;
using System.Collections.Generic;
using Rage;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace TextDispatch.Chat
{
    /// <summary>
    /// The mobile data computer: a screen in the middle of the view, with tabs - people, vehicles,
    /// warrants, BOLOs, the dispatch screen, the units, reports, cases, flags, messages.
    ///
    /// It is still text. Everything on it is what the box's own lookups say, caught on the way to the box
    /// (ChatBox.Capture) and shown here instead, so the computer and the commands can never disagree. It is
    /// driven from the chat box: /mdt and a tab, or - while it is open - a name or a plate typed on its own.
    ///
    /// Drawn in the render callback from a snapshot; filled on the plugin's fiber.
    /// </summary>
    public sealed class Computer
    {
        public static readonly string[] Tabs = { "HOME", "PERSON", "VEHICLE", "WARRANTS", "BOLO", "CAD", "UNITS", "REPORTS", "CASES", "FLAGS", "MSG" };

        private readonly object _gate = new object();
        private List<ChatLine> _lines = new List<ChatLine>();
        private string _tab = "HOME";
        private string _title = "";
        private int _page;

        public bool Open;
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

        private const int Rows = 22;

        public void Render(Graphics graphics, string font, float scale)
        {
            if (!Open) return;

            string tab, title;
            ChatLine[] lines;
            int page;
            lock (_gate)
            {
                tab = _tab;
                title = _title;
                lines = _lines.ToArray();
                page = _page;
            }

            float screenWidth = 1920f, screenHeight = 1080f;
            try
            {
                var resolution = Game.Resolution;
                if (resolution.Width > 0f && resolution.Height > 0f) { screenWidth = resolution.Width; screenHeight = resolution.Height; }
            }
            catch { }

            var size = 15f * (scale <= 0 ? 1f : scale);
            var row = size * 1.5f;
            var width = Math.Min(screenWidth * 0.62f, 1060f * scale);
            var height = row * (Rows + 5) + 20f;
            var x = (screenWidth - width) / 2f;
            var y = Math.Max(20f, (screenHeight - height) / 2f);

            // The case, the screen, the header bar.
            graphics.DrawRectangle(new RectangleF(x - 10f, y - 10f, width + 20f, height + 20f), Color.FromArgb(235, 28, 30, 34));
            graphics.DrawRectangle(new RectangleF(x, y, width, height), Color.FromArgb(240, 8, 20, 38));
            graphics.DrawRectangle(new RectangleF(x, y, width, row + 6f), Color.FromArgb(255, 22, 58, 110));
            graphics.DrawText("LSPD MOBILE DATA COMPUTER", font, size, new PointF(x + 10f, y + 3f), Color.FromArgb(255, 235, 240, 250));
            var right = Unit + "   " + DateTime.Now.ToString("HH:mm");
            graphics.DrawText(right, font, size, new PointF(x + width - Measure(right, font, size) - 12f, y + 3f), Color.FromArgb(255, 200, 215, 235));

            // The tab bar: the current tab lit.
            var tx = x + 8f;
            var ty = y + row + 12f;
            foreach (var name in Tabs)
            {
                var w = Measure(name, font, size * 0.85f) + 14f;
                var lit = name == tab;
                graphics.DrawRectangle(new RectangleF(tx, ty, w, row), lit ? Color.FromArgb(255, 60, 120, 200) : Color.FromArgb(255, 26, 40, 62));
                graphics.DrawText(name, font, size * 0.85f, new PointF(tx + 7f, ty + 3f), lit ? Color.White : Color.FromArgb(255, 150, 170, 195));
                tx += w + 4f;
            }

            // The screen itself.
            var cy = ty + row + 10f;
            graphics.DrawText(title, font, size * 1.05f, new PointF(x + 12f, cy), Color.FromArgb(255, 120, 200, 255));
            cy += row + 4f;

            var pages = Math.Max(1, (lines.Length + Rows - 2) / (Rows - 1));
            page = Math.Min(page, pages - 1);
            var first = page * (Rows - 1);
            var clip = new RectangleF(x + 8f, cy, width - 16f, row * Rows);
            for (int i = first; i < lines.Length && i < first + Rows - 1; i++)
            {
                var line = lines[i];
                var text = string.IsNullOrEmpty(line.Tag) ? line.Text : line.Tag + " " + line.Text;
                graphics.DrawText(text, font, size, new PointF(x + 12f, cy), ColourOf(line), new RectangleF(x + 8f, cy - 2f, width - 16f, row + 4f));
                cy += row;
            }

            var footer = "Type a name or a plate to search   /mdt <tab>   /mdt next | back   /mdt close" +
                         (pages > 1 ? "      page " + (page + 1) + " of " + pages : "");
            graphics.DrawText(footer, font, size * 0.85f, new PointF(x + 12f, y + height - row - 4f), Color.FromArgb(255, 130, 150, 175));
        }

        private static Color ColourOf(ChatLine line)
        {
            var t = line.Text ?? "";
            if (line.Channel == ChatChannel.Error || t.Contains("WARRANT") || t.Contains("FLAG") || t.Contains("STOLEN") || t.Contains("SUSPENDED") || t.Contains("WANTED"))
                return Color.FromArgb(255, 255, 120, 110);
            if (t.Contains("BOLO")) return Color.FromArgb(255, 255, 200, 90);
            if (line.Channel == ChatChannel.Phone) return Color.FromArgb(255, 245, 215, 110);
            if (line.Channel == ChatChannel.Dispatch || line.Channel == ChatChannel.Radio) return Color.FromArgb(255, 122, 200, 255);
            return t.StartsWith("  ") ? Color.FromArgb(255, 200, 215, 230) : Color.FromArgb(255, 240, 245, 250);
        }

        private static float Measure(string text, string font, float size)
        {
            try { return Graphics.MeasureText(text, font, size).Width; }
            catch { return text.Length * size * 0.55f; }
        }
    }
}
