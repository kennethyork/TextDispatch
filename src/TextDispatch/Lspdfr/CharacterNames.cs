using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// The preset characters, and their names.
    ///
    /// The characters offered at a police station are LSPDFR's preset characters, and the name the menu
    /// shows for one is that character's &lt;Name&gt; in lspdfr\data\cop_presets.xml. Naming one is
    /// therefore editing that file, which is what /chars and /rename do from the box - so it does not
    /// need the game closed and a text editor open.
    ///
    /// The file holds every character at once, so two rules: the copy is kept beside it before anything
    /// is written, and the result is parsed again afterwards - if it no longer parses, the copy goes
    /// back and nothing has changed. The &lt;ScriptName&gt; is never touched: it is what the game and
    /// the save files know the character by, and it is never shown to the player.
    ///
    /// A custom character is named in the game's own character creator; these are the presets.
    /// </summary>
    internal static class CharacterNames
    {
        public sealed class Character
        {
            public int Number;
            public string Name;
            public string ScriptName;
            public string Agency;
            public string Model;
            public string Block;
        }

        private const string Missing = "no file at lspdfr\\data\\cop_presets.xml";

        private static List<Character> _characters;
        private static string _location;
        private static string _problem;
        private static DateTime _stamp = DateTime.MinValue;

        public static string Problem { get { return _problem; } }
        public static string Location { get { return _location; } }

        public static List<Character> All()
        {
            Load();
            return _characters ?? new List<Character>();
        }

        /// <summary>Rename the character /chars printed under this number.</summary>
        public static Character Rename(int number, string newName, out string problem)
        {
            problem = null;
            var characters = All();
            if (characters.Count == 0) { problem = Problem ?? Missing; return null; }
            if (number < 1 || number > characters.Count)
            {
                problem = "there is no character " + number + " - /chars lists them";
                return null;
            }
            return Rename(characters[number - 1], newName, out problem);
        }

        /// <summary>Rename by the name it has now, or by its script name.</summary>
        public static Character Rename(string current, string newName, out string problem)
        {
            problem = null;
            var characters = All();
            if (characters.Count == 0) { problem = Problem ?? Missing; return null; }

            var wanted = (current ?? "").Trim();
            foreach (var character in characters)
                if (string.Equals(character.Name, wanted, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(character.ScriptName, wanted, StringComparison.OrdinalIgnoreCase))
                    return Rename(character, newName, out problem);

            problem = "no character called '" + wanted + "' - /chars lists them";
            return null;
        }

        private static Character Rename(Character character, string newName, out string problem)
        {
            problem = null;

            var name = (newName ?? "").Trim();
            if (name.Length == 0) { problem = "no name was given"; return null; }
            if (name.Length > 32)
            {
                problem = "that name is " + name.Length + " characters - 32 is the most the menu shows without cutting it";
                return null;
            }

            var path = Find();
            if (path == null) { problem = Missing; return null; }

            string updated, backup;
            try
            {
                var text = File.ReadAllText(path);
                if (text.IndexOf(character.Block, StringComparison.Ordinal) < 0)
                {
                    problem = "the file changed since the list was read - run /chars again";
                    return null;
                }

                var block = new Regex("<Name>[^<]*</Name>").Replace(character.Block,
                    "<Name>" + SecurityElement.Escape(name) + "</Name>", 1);
                updated = text.Replace(character.Block, block);
                backup = path + ".bak-names-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(path, backup, false);
                File.WriteAllText(path, updated, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                problem = ex.Message;
                return null;
            }

            string why;
            if (!Reads(path, out why))
            {
                try { File.Copy(backup, path, true); } catch { }
                problem = "the edit left the file unreadable (" + why + "), so the copy was put back: " + Path.GetFileName(backup);
                return null;
            }

            // Read it again next time, so /chars shows the new name straight away.
            _characters = null;
            _stamp = DateTime.MinValue;
            Log.Line("characters: '" + character.Name + "' renamed to '" + name + "'  (backup " + Path.GetFileName(backup) + ")");
            return character;
        }

        /// <summary>
        /// Parsed and counted after a write. Parsing is not proof on its own that nothing was lost, so
        /// the characters are counted too.
        /// </summary>
        private static bool Reads(string path, out string why)
        {
            why = null;
            try
            {
                var document = new XmlDocument();
                document.Load(path);
                var presets = document.SelectNodes("//Preset");
                if (presets == null || presets.Count == 0) { why = "no characters in it"; return false; }
                return true;
            }
            catch (Exception ex) { why = ex.Message; return false; }
        }

        private static void Load()
        {
            var path = Find();
            if (path == null) { _characters = null; return; }

            try
            {
                if (_characters != null &&
                    string.Equals(path, _location, StringComparison.OrdinalIgnoreCase) &&
                    File.GetLastWriteTimeUtc(path) == _stamp) return;

                var text = File.ReadAllText(path);
                var list = new List<Character>();
                foreach (Match block in Regex.Matches(text, "(?s)<Preset>.*?</Preset>"))
                {
                    list.Add(new Character
                    {
                        Number = list.Count + 1,
                        Name = Field(block.Value, "Name"),
                        ScriptName = Field(block.Value, "ScriptName"),
                        Agency = Field(block.Value, "Agency"),
                        Model = Field(block.Value, "Model"),
                        Block = block.Value
                    });
                }

                _characters = list;
                _location = path;
                _stamp = File.GetLastWriteTimeUtc(path);
                _problem = list.Count == 0 ? "no <Preset> elements in " + path : null;
            }
            catch (Exception ex)
            {
                _characters = null;
                _problem = ex.Message;
            }
        }

        private static string Field(string block, string element)
        {
            var match = Regex.Match(block, "<" + element + ">([^<]*)</" + element + ">");
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private static string Find()
        {
            if (_location != null && File.Exists(_location)) return _location;

            foreach (var candidate in Candidates())
            {
                try { if (File.Exists(candidate)) { _location = candidate; return candidate; } }
                catch { }
            }

            _problem = Missing;
            return null;
        }

        private static IEnumerable<string> Candidates()
        {
            var pluginFolder = Settings.PluginFolder();
            var relative = Path.Combine("lspdfr", "data", "cop_presets.xml");

            yield return Path.Combine(GameFolder(pluginFolder), relative);
            yield return Path.Combine(pluginFolder, "..", "..", relative);
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", relative);
            yield return Path.Combine(Environment.CurrentDirectory, relative);
        }

        /// <summary>
        /// Plugins\LSPDFR -> the folder holding GTA5.exe. The plugin's own folder is asked for rather
        /// than assumed, because LSPDFR loads plugins from memory and an assembly loaded that way
        /// cannot say where it is.
        /// </summary>
        private static string GameFolder(string pluginFolder)
        {
            try
            {
                var parent = Directory.GetParent(pluginFolder);
                if (parent != null && parent.Parent != null) return parent.Parent.FullName;
            }
            catch { }
            return Environment.CurrentDirectory;
        }
    }
}
