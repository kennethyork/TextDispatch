using System;
using System.Reflection;
using Rage;

namespace TextCallouts
{
    /// <summary>
    /// Every line the pack says to the player goes through here, so it appears in one place.
    ///
    /// It always shows a notification. If TextDispatch is installed, the same line also goes into its
    /// chat box - which is the whole point of a text dispatcher - and that is done by reflection, so
    /// this pack has no dependency on it: no reference at build time, no file to install, and no
    /// failure when it is absent. Both plugins live in LSPDFR's AppDomain, which is what makes the
    /// call possible at all.
    /// </summary>
    internal static class Hud
    {
        private static bool _bound;
        private static object _chat;
        private static MethodInfo _notice;

        public static void Say(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            try { Game.DisplayNotification(line); }
            catch (Exception ex) { Log.Error("notification", ex); }

            ToChatBox(line);
        }

        /// <summary>The small help box in the top-left, for something the player has to do now.</summary>
        public static void Help(string line, int milliseconds = 6000)
        {
            try { Game.DisplayHelp(line, milliseconds); }
            catch { }
        }

        private static void ToChatBox(string line)
        {
            if (!_bound) Bind();
            if (_notice == null || _chat == null) return;

            try { _notice.Invoke(_chat, new object[] { line }); }
            catch { _notice = null; }      // it changed under us; stop trying rather than spam the log
        }

        private static void Bind()
        {
            _bound = true;
            try
            {
                var plugin = Type.GetType("TextDispatch.Plugin, TextDispatch", false);
                if (plugin == null) return;

                var chatProperty = plugin.GetProperty("Chat", BindingFlags.Public | BindingFlags.Static);
                if (chatProperty == null) return;

                _chat = chatProperty.GetValue(null, null);
                if (_chat == null) return;      // TextDispatch is loaded but not started yet

                _notice = _chat.GetType().GetMethod("Notice", new[] { typeof(string) });
                if (_notice == null) { _chat = null; return; }

                Log.Line("text bridge: TextDispatch found - callout lines will also appear in its chat box");
            }
            catch (Exception ex)
            {
                _chat = null;
                _notice = null;
                Log.Error("text bridge", ex);
            }
        }
    }
}
