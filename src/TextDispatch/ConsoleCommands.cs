using System;
using Rage;
using Rage.Attributes;

namespace TextDispatch
{
    /// <summary>
    /// F4 console commands.
    ///
    /// Their real job is insurance: if the chat box draws wrongly - the wrong size, an unreadable
    /// font - these are how it gets fixed without a rebuild, and how anything gets said if the box
    /// cannot be typed into at all.
    /// </summary>
    public static class ConsoleCommands
    {
        [ConsoleCommand("tdsay", Description = "TextDispatch: send a line as if typed into the chat box. e.g. tdsay /accept")]
        public static void TdSay(string text)
        {
            if (Plugin.Chat == null) { Game.Console.Print("[TextDispatch] not running."); return; }
            if (string.IsNullOrWhiteSpace(text))
            {
                Game.Console.Print("[TextDispatch] usage: tdsay <text>   e.g. tdsay 10-97");
                return;
            }
            Plugin.Chat.Notice("> " + text);
            Plugin.Router.Handle(text);
        }

        [ConsoleCommand("tdstatus", Description = "TextDispatch: show LSPDFR, chat box and callout state.")]
        public static void TdStatus()
        {
            if (Plugin.Chat == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            Game.Console.Print("[TextDispatch] LSPDFR: " + Plugin.Api.Describe());
            var settings = Plugin.Settings;
            Game.Console.Print("[TextDispatch] AI: mode=" + settings.AiMode + " provider=" + settings.AiProvider +
                               " -> " + (settings.UseModel ? "model" : "script") +
                               " at " + TextDispatch.Ai.LocalModel.Endpoint(settings));
            if (settings.UseModel && !string.IsNullOrEmpty(settings.ResolvedModel))
                Game.Console.Print("[TextDispatch] model: " + settings.ResolvedModel);
            if ((Plugin.Dialogue != null && Plugin.Dialogue.ModelBusy) ||
                (Plugin.Dispatch != null && Plugin.Dispatch.ModelBusy))
                Game.Console.Print("[TextDispatch] a model turn is in flight.");
            Game.Console.Print("[TextDispatch] box: " + Plugin.Chat.LineCount + " lines, " +
                               Plugin.Chat.VisibleLines + " shown, scale " + Plugin.Chat.UiScale +
                               ", font " + Plugin.Chat.FontName + " " + Plugin.Chat.FontSize);
            Game.Console.Print("[TextDispatch] log: " + Log.Path);
        }

        [ConsoleCommand("tdscale", Description = "TextDispatch: resize the chat box. e.g. tdscale 0.8")]
        public static void TdScale(string value)
        {
            if (Plugin.Chat == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            float scale;
            if (!float.TryParse(value, out scale) || scale <= 0f)
            {
                Game.Console.Print("[TextDispatch] usage: tdscale <scale>   e.g. tdscale 0.8");
                return;
            }

            Plugin.Chat.UiScale = scale;
            Game.Console.Print("[TextDispatch] scale " + scale);
        }

        [ConsoleCommand("tdfontsize", Description = "TextDispatch: set the chat font size. e.g. tdfontsize 16")]
        public static void TdFontSize(string value)
        {
            if (Plugin.Chat == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            float size;
            if (!float.TryParse(value, out size) || size <= 0f)
            {
                Game.Console.Print("[TextDispatch] usage: tdfontsize <size>   e.g. tdfontsize 16");
                return;
            }

            Plugin.Chat.FontSize = size;
            Game.Console.Print("[TextDispatch] font size " + size);
        }

        [ConsoleCommand("tdlines", Description = "TextDispatch: how many chat lines to show. e.g. tdlines 12")]
        public static void TdLines(string value)
        {
            if (Plugin.Chat == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            int lines;
            if (!int.TryParse(value, out lines) || lines < 3 || lines > 40)
            {
                Game.Console.Print("[TextDispatch] usage: tdlines <3-40>   e.g. tdlines 12");
                return;
            }

            Plugin.Chat.VisibleLines = lines;
            Game.Console.Print("[TextDispatch] showing " + lines + " lines");
        }

        [ConsoleCommand("tdfont", Description = "TextDispatch: change the chat font if text does not draw. e.g. tdfont Consolas")]
        public static void TdFont(string value)
        {
            if (Plugin.Chat == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            if (string.IsNullOrWhiteSpace(value))
            {
                Game.Console.Print("[TextDispatch] current font: " + Plugin.Chat.FontName);
                Game.Console.Print("[TextDispatch] usage: tdfont <name>   e.g. tdfont Consolas, tdfont \"Segoe UI\"");
                return;
            }

            Plugin.Chat.FontName = value.Trim();
            Game.Console.Print("[TextDispatch] font is now " + Plugin.Chat.FontName);

            // If the box is not drawing, this is the only way to find a font that does.
            try
            {
                var size = Rage.Graphics.MeasureText("TextDispatch", Plugin.Chat.FontName, Plugin.Chat.FontSize);
                Game.Console.Print("[TextDispatch] it measures " + size.Width.ToString("0.0") + "x" +
                                   size.Height.ToString("0.0") +
                                   (size.Width <= 0f ? "   <-- still not available, try Consolas" : "   <-- good"));
            }
            catch (Exception ex) { Game.Console.Print("[TextDispatch] measurement failed: " + ex.Message); }

            Log.Line("font changed to " + Plugin.Chat.FontName + " (not saved to the ini)");
        }

        [ConsoleCommand("tdrender", Description = "TextDispatch: is the chat box actually being drawn, and did the font load?")]
        public static void TdRender()
        {
            if (Plugin.Chat == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            Game.Console.Print("[TextDispatch] FrameRender callbacks so far: " + Plugin.RenderCalls +
                               (Plugin.RenderCalls == 0 ? "   <-- NOTHING IS BEING DRAWN" : ""));
            Game.Console.Print("[TextDispatch] last render error: " + (Plugin.LastRenderError ?? "none"));
            Game.Console.Print("[TextDispatch] box: " + Plugin.Chat.LineCount + " lines, " +
                               Plugin.Chat.VisibleLines + " shown, scale " + Plugin.Chat.UiScale +
                               ", font " + Plugin.Chat.FontName + " " + Plugin.Chat.FontSize);

            try
            {
                var size = Rage.Graphics.MeasureText("TextDispatch", Plugin.Chat.FontName, Plugin.Chat.FontSize);
                Game.Console.Print("[TextDispatch] font measures as " + size.Width.ToString("0.0") + "x" +
                                   size.Height.ToString("0.0") +
                                   (size.Width <= 0f ? "   <-- FONT NOT AVAILABLE, try: tdfont Consolas" : ""));
            }
            catch (Exception ex) { Game.Console.Print("[TextDispatch] font measurement failed: " + ex.Message); }
        }

        [ConsoleCommand("tdkey", Description = "TextDispatch: which key opens the chat box. e.g. tdkey F6")]
        public static void TdKey(string value)
        {
            if (Plugin.Input == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            if (string.IsNullOrWhiteSpace(value))
            {
                Game.Console.Print("[TextDispatch] the chat box opens with '" + Plugin.Settings.OpenKey +
                                   "' (or '/' to start a command).");
                Game.Console.Print("[TextDispatch] usage: tdkey <key name>   e.g. tdkey F6, tdkey Home");
                return;
            }

            if (!Plugin.Input.SetOpenKey(value))
            {
                Game.Console.Print("[TextDispatch] '" + value + "' is not a key name. Try T, F6, Home, OemQuestion.");
                return;
            }

            Plugin.Settings.OpenKey = value.Trim();
            Game.Console.Print("[TextDispatch] the chat box now opens with " + Plugin.Settings.OpenKey + ".");
            Log.Line("open key changed to " + Plugin.Settings.OpenKey + " (not saved to the ini)");
        }

        [ConsoleCommand("tdwho", Description = "TextDispatch: list the people near you, and which one you are talking to.")]
        public static void TdWho()
        {
            if (Plugin.Dialogue == null) { Game.Console.Print("[TextDispatch] not running."); return; }
            Plugin.Dialogue.WhoIsAround();
        }

        [ConsoleCommand("tdmode", Description = "TextDispatch: AI mode - tdmode auto | tdmode llm | tdmode scripted.")]
        public static void TdMode(string value)
        {
            if (Plugin.Settings == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            var mode = (value ?? "").Trim().ToLowerInvariant();
            if (mode != "auto" && mode != "llm" && mode != "scripted")
            {
                Game.Console.Print("[TextDispatch] usage: tdmode auto | tdmode llm | tdmode scripted");
                return;
            }

            Plugin.Settings.ForceMode(mode);
            Game.Console.Print("[TextDispatch] AiMode=" + mode + " -> " +
                               (Plugin.Settings.UseModel ? "using a model" : "using the script"));
            Log.Line("ai mode changed to " + mode + " (not saved to the ini)");
        }

        [ConsoleCommand("tdmodels", Description = "TextDispatch: ask the local model server what models it has.")]
        public static void TdModels()
        {
            if (Plugin.Settings == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            var settings = Plugin.Settings;
            Game.Console.Print("[TextDispatch] provider " + settings.AiProvider +
                               "  endpoint " + TextDispatch.Ai.LocalModel.Endpoint(settings));

            string error;
            var models = TextDispatch.Ai.LocalModel.Models(settings, out error);
            if (models.Count == 0)
            {
                Game.Console.Print("[TextDispatch] no models found" + (error == null ? "." : ": " + error));
                Game.Console.Print("[TextDispatch] for Ollama, is it running?  ollama serve   /   ollama list");
                return;
            }

            Game.Console.Print("[TextDispatch] " + models.Count + " model(s); blank AiModel uses the first:");
            foreach (var model in models)
            {
                Game.Console.Print("   " + model);
                Log.Line("model offered: " + model);
            }
        }

        [ConsoleCommand("tdcallouts", Description = "TextDispatch: list every callout this install has, and log them.")]
        public static void TdCallouts()
        {
            if (Plugin.Api == null) { Game.Console.Print("[TextDispatch] not running."); return; }

            var packs = Plugin.Api.CalloutPacks();
            Game.Console.Print("[TextDispatch] " + Plugin.Api.CalloutCount + " callouts from " +
                               packs.Count + " pack(s): " + string.Join(", ", packs.ToArray()));

            var names = Plugin.Api.ListCallouts();
            if (names.Count == 0)
            {
                Game.Console.Print("[TextDispatch] no callouts found - is LSPDFR loaded?");
                return;
            }

            foreach (var name in names)
            {
                Game.Console.Print("   " + name);
                Log.Line("callout: " + name);
            }
        }
    }
}
