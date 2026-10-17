using System;
using Rage;
using Rage.Native;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace TextDispatch.Chat
{
    /// <summary>
    /// /ame: an action shown above your head for a few seconds, the way GTA World draws one, as well as in
    /// the box. Somebody standing in front of you sees "* checks the plate" over you rather than in a log.
    ///
    /// Where your head is on screen needs a native call, and the render callback is not allowed to make
    /// one - so the position is worked out on the plugin's fiber each tick and only drawn at render. It is
    /// a tick behind, which at walking pace is nothing.
    /// </summary>
    internal static class Overhead
    {
        private static readonly object Gate = new object();
        private static string _text;
        private static int _until;
        private static float _x, _y;      // 0..1 of the screen
        private static bool _onScreen;
        private static bool _failed;

        public static void Show(string text, int seconds)
        {
            lock (Gate)
            {
                _text = text;
                _until = Environment.TickCount + Math.Max(2, seconds) * 1000;
            }
        }

        /// <summary>Called from the fiber: where above the player's head is on screen.</summary>
        public static void Update()
        {
            string text;
            lock (Gate) text = _text;
            if (text == null) return;

            if (Environment.TickCount - _until > 0)
            {
                lock (Gate) { _text = null; _onScreen = false; }
                return;
            }
            if (_failed) return;

            try
            {
                var player = Game.LocalPlayer.Character;
                if (player == null || !player.Exists()) { _onScreen = false; return; }

                var head = player.Position + new Vector3(0f, 0f, 1.05f);
                float x, y;
                bool visible = NativeFunction.Natives.GET_SCREEN_COORD_FROM_WORLD_COORD<bool>(head.X, head.Y, head.Z, out x, out y);
                lock (Gate) { _x = x; _y = y; _onScreen = visible && x > 0f && x < 1f && y > 0f && y < 1f; }
            }
            catch (Exception ex)
            {
                // Reported once: the action is still in the box, which is where it matters most.
                _failed = true;
                Log.Error("overhead text position (the /ame line still goes in the box)", ex);
            }
        }

        /// <summary>Called from the render callback. Managed drawing only.</summary>
        public static void Render(Graphics graphics, string font, float size)
        {
            string text;
            float x, y;
            lock (Gate)
            {
                if (_text == null || !_onScreen) return;
                text = _text;
                x = _x;
                y = _y;
            }

            float width = 1920f, height = 1080f;
            try
            {
                var resolution = Game.Resolution;
                if (resolution.Width > 0f && resolution.Height > 0f) { width = resolution.Width; height = resolution.Height; }
            }
            catch { }

            var shown = "* " + text;
            float measured = shown.Length * size * 0.5f;
            try { measured = Graphics.MeasureText(shown, font, size).Width; } catch { }

            var px = x * width - measured / 2f;
            var py = y * height - size * 1.6f;
            graphics.DrawRectangle(new RectangleF(px - 6f, py - 3f, measured + 12f, size * 1.5f + 4f), Color.FromArgb(110, 0, 0, 0));
            graphics.DrawText(shown, font, size, new PointF(px, py), Color.FromArgb(255, 214, 152, 255));
        }
    }
}
