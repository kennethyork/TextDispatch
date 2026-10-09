using System;
using System.Collections.Generic;

namespace TextDispatch.Commands
{
    /// <summary>
    /// Tab completion for the box.
    ///
    /// The first word of a command completes against every command the router knows; after it, the
    /// commands that take a name complete against the names they take - a callout, a person on file, a
    /// corner of the screen. Repeated Tab goes through the matches in turn, the way a shell does, and
    /// Shift+Tab goes back. No reference to the game, so it can be checked on its own.
    /// </summary>
    internal sealed class Completion
    {
        /// <summary>Every command name and alias in the router's switch, without the slash.</summary>
        public static readonly string[] Commands =
        {
            "help", "me", "do", "b", "ooc", "r", "radio", "s", "say", "w", "whisper", "shout", "y", "yell",
            "who", "talk", "endtalk", "accept", "yes", "decline", "no", "key", "chatter", "spam", "typing",
            "callout", "start", "calls", "auto", "jobs", "job", "endcall", "available", "backup", "ems",
            "ambulance", "medic", "fire", "firedept", "lsfd", "stop", "pullover", "endstop", "tow", "impound",
            "transport", "detain", "release", "uncuff", "cuff", "frisk", "search", "searchcar", "searchveh",
            "vsearch", "id", "licence", "license", "record", "owner", "zone", "lock", "unlock", "engine", "trunk",
            "hood", "doors", "shut", "repair", "fix", "fixveh", "veh", "spawn", "mdt", "terminal", "person",
            "name", "plate", "warrant", "wants", "bolo", "arrest", "cite", "ticket", "report", "reports",
            "court", "followups", "status", "shift", "shifts", "evidence", "miranda", "rights", "interview",
            "question", "fines", "units", "warrants", "pursuit", "endpursuit", "calledin", "panic", "911", "k9",
            "dog", "spikes", "spikestrips", "stingers", "roadblock", "block", "pit", "felony", "felonystop",
            "coroner", "animal", "animalcontrol", "group", "dismiss", "standdown", "platecheck", "runplate",
            "pedcheck", "runped", "insurance", "reg", "registration", "breath", "breathalyzer", "dui", "drugs",
            "bridges", "frameworks", "chars", "characters", "rename", "plugins", "plugin", "pos", "corner",
            "margin", "ui", "font", "fontsize", "lines", "clear", "cls"
        };

        /// <summary>What a command's argument can be completed from: "callouts", "people", or a fixed list.</summary>
        public static string ArgumentKind(string command)
        {
            switch ((command ?? "").ToLowerInvariant())
            {
                case "callout": case "start": case "calls": return "callouts";
                case "person": case "name": case "warrant": case "wants": case "arrest": case "cite": case "ticket":
                case "reports": case "talk":
                    return "people";
                case "pos": case "corner": return "top-right|top-left|bottom-right|bottom-left";
                case "chatter": case "spam": return "quiet|brief|full";
                case "auto": return "on|off|accept on|accept off";
                case "available": case "typing": return "on|off";
                case "backup": return "swat|air|state|ems|fire|transport|code2|k9|spikes|roadblock";
                case "search": return "car";
                case "bolo": return "list|add|clear";
                case "report": return "draft|general:";
                case "evidence": return "list";
                case "fines": return "speeding|insurance|parking|red light";
                default: return null;
            }
        }

        private readonly Func<string, IEnumerable<string>> _source;

        // The cycle in progress: what was typed before the first Tab, and the matches for it.
        private string _stem;
        private List<string> _matches;
        private int _index;
        private string _shown;

        /// <param name="source">Candidates for an argument kind: "callouts" or "people".</param>
        public Completion(Func<string, IEnumerable<string>> source)
        {
            _source = source;
        }

        /// <summary>
        /// The input with the next completion in place, or the input unchanged when nothing matches.
        /// `matches` is every candidate on the first press of a cycle, so the caller can show them.
        /// </summary>
        public string Next(string input, bool backwards, out List<string> matches)
        {
            matches = null;
            input = input ?? "";

            // Still cycling: the box shows the last completion we produced.
            if (_matches != null && input == _shown && _matches.Count > 0)
            {
                _index = backwards ? (_index - 1 + _matches.Count) % _matches.Count : (_index + 1) % _matches.Count;
                _shown = _stem + _matches[_index];
                return _shown;
            }

            string stem, word;
            var found = Candidates(input, out stem, out word);
            if (found.Count == 0) { Reset(); return input; }

            _stem = stem;
            _matches = found;
            _index = backwards ? found.Count - 1 : 0;
            _shown = stem + found[_index];
            if (found.Count > 1) matches = found;
            return _shown;
        }

        public void Reset()
        {
            _matches = null;
            _stem = null;
            _shown = null;
        }

        /// <summary>
        /// What the last word of the input could be. `stem` is everything before it, so stem + a match
        /// is the completed input; a single command match gets a trailing space, ready for its argument.
        /// </summary>
        public List<string> Candidates(string input, out string stem, out string word)
        {
            var results = new List<string>();
            stem = "";
            word = "";

            if (!input.StartsWith("/")) return results;

            var space = input.IndexOf(' ');
            if (space < 0)
            {
                // The command itself.
                stem = "/";
                word = input.Substring(1).ToLowerInvariant();
                foreach (var command in Commands)
                    if (command.StartsWith(word, StringComparison.Ordinal) && !results.Contains(command)) results.Add(command);
                results.Sort(StringComparer.Ordinal);
                if (results.Count == 1) results[0] += " ";
                return results;
            }

            // Its argument: the whole of it, since callout and person names have spaces in them.
            var name = input.Substring(1, space - 1);
            var kind = ArgumentKind(name);
            if (kind == null) return results;

            stem = input.Substring(0, space + 1);
            word = input.Substring(space + 1);

            IEnumerable<string> pool;
            if (kind == "callouts" || kind == "people") pool = _source == null ? null : _source(kind);
            else pool = kind.Split('|');
            if (pool == null) return results;

            foreach (var candidate in pool)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                if (candidate.StartsWith(word, StringComparison.OrdinalIgnoreCase) && !results.Contains(candidate)) results.Add(candidate);
            }

            // And anything with the typed text inside it, after the ones that start with it - "robbery"
            // finds "Armed Robbery", which is how somebody remembers a callout.
            if (word.Length >= 3)
                foreach (var candidate in pool)
                {
                    if (string.IsNullOrEmpty(candidate) || results.Contains(candidate)) continue;
                    if (candidate.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(candidate);
                }

            if (results.Count > 60) results.RemoveRange(60, results.Count - 60);
            return results;
        }
    }
}
