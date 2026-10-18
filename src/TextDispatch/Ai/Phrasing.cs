using System;
using System.Collections.Generic;

namespace TextDispatch.Ai
{
    /// <summary>
    /// The model putting a line into somebody's own words - a sergeant, a detective, the dispatcher reading a
    /// plate back - without being allowed to change what it says.
    ///
    /// The facts come from the records and are fixed before the model sees them. What comes back is checked:
    /// every number, every plate and every word in capitals in the original has to be in the reply, and a
    /// reply that drops one, or runs on, is thrown away for the original. So a plate is never misread and a
    /// STOLEN never goes missing, however small the model.
    /// </summary>
    internal static class Phrasing
    {
        /// <summary>The model's version of `facts`, spoken as `speaker`; null when it failed or changed a fact.</summary>
        public static string Reword(Settings settings, string speaker, string style, string facts)
        {
            if (settings == null || !settings.UseModel || string.IsNullOrWhiteSpace(facts)) return null;

            var system = "You are " + speaker + ". " + style + "\n" +
                         "Rules:\n" +
                         "- Say the message below in your own words, the way you would really say it.\n" +
                         "- Keep every fact, name, number and plate exactly as written, and every word in CAPITALS in capitals.\n" +
                         "- Add nothing that is not in the message. No new facts, people or places.\n" +
                         "- One to three short sentences. Write only what you say: no quotation marks, no label, no narration.\n" +
                         "- Never mention being an AI, a model, or a game.";
            string error;
            var reply = LocalModel.Chat(settings, system, "The message: " + facts, out error);
            if (reply == null)
            {
                if (error != null) Log.Line("phrasing model: " + error);
                return null;
            }
            reply = reply.Trim().Trim('"').Trim();
            if (!Keeps(facts, reply, speaker + " " + style))
            {
                Log.Line("phrasing: the model's version lost a fact, so the original was used - " + reply);
                return null;
            }
            return reply;
        }

        /// <summary>Whether the reply still carries every number, plate and capitalised word of the original.</summary>
        public static bool Keeps(string original, string reply, string allowed = null)
        {
            if (string.IsNullOrWhiteSpace(reply)) return false;
            if (reply.Length > Math.Max(80, original.Length * 3)) return false;

            foreach (var token in Tokens(original))
            {
                var mustKeep = false;
                foreach (var c in token) if (char.IsDigit(c)) { mustKeep = true; break; }
                if (!mustKeep && token.Length >= 3 && token == token.ToUpperInvariant() && HasLetter(token)) mustKeep = true;
                if (mustKeep && reply.IndexOf(token, StringComparison.Ordinal) < 0) return false;
            }

            // And nothing invented: a number or a plate in the reply that the original does not have - a second
            // car, a time, an address - is a fact the records never gave it.
            foreach (var token in Tokens(reply))
            {
                var hasDigit = false;
                foreach (var c in token) if (char.IsDigit(c)) { hasDigit = true; break; }
                if (hasDigit && original.IndexOf(token, StringComparison.Ordinal) < 0 &&
                    (allowed == null || allowed.IndexOf(token, StringComparison.Ordinal) < 0)) return false;
            }
            return true;
        }

        private static IEnumerable<string> Tokens(string text)
        {
            foreach (var raw in (text ?? "").Split(new[] { ' ', ',', '.', ';', ':', '!', '?', '(', ')', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim('-', '/', '#');
                if (token.Length > 0) yield return token;
            }
        }

        private static bool HasLetter(string text)
        {
            foreach (var c in text) if (char.IsLetter(c)) return true;
            return false;
        }
    }
}
