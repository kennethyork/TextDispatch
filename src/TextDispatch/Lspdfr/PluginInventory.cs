using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TextDispatch.Lspdfr
{
    internal enum PluginState
    {
        /// <summary>LSPDFR lists it among its loaded plugins.</summary>
        Loaded,

        /// <summary>
        /// The AppDomain holds the assembly, but LSPDFR does not list it. Usually the same thing
        /// seen from a different angle; on an LSPDFR build with no plugin list, it is all we have.
        /// </summary>
        InDomain,

        /// <summary>Sitting in Plugins\LSPDFR, and LSPDFR never loaded it.</summary>
        NotLoaded,

        /// <summary>In the folder but not an LSPDFR plugin - a dependency dropped in alongside one.</summary>
        Library,

        /// <summary>Not a managed assembly at all, so it is none of our business.</summary>
        Unreadable
    }

    internal sealed class PluginEntry
    {
        public string Name;
        public string Version;
        public PluginState State;

        /// <summary>Assemblies it needs that are nowhere to be found on this machine.</summary>
        public readonly List<string> Missing = new List<string>();

        /// <summary>
        /// References with obfuscated names. Packs that merge their dependencies into one DLL -
        /// ConfuserEx, Costura - end up referring to something whose name is a random string, and it
        /// resolves out of the pack's own resources at runtime. Reporting those as missing would
        /// bury the real ones.
        /// </summary>
        public readonly List<string> Merged = new List<string>();

        public bool IsPlugin { get { return State != PluginState.Library && State != PluginState.Unreadable; } }

        public bool IsLoaded { get { return State == PluginState.Loaded || State == PluginState.InDomain; } }

        public string StateWord
        {
            get
            {
                switch (State)
                {
                    case PluginState.Loaded: return "loaded";
                    case PluginState.InDomain: return "loaded (LSPDFR did not list it)";
                    case PluginState.NotLoaded: return "NOT LOADED";
                    case PluginState.Library: return "a library, not a plugin";
                    default: return "not a managed assembly";
                }
            }
        }
    }

    /// <summary>
    /// What LSPDFR actually loaded, and what it did not.
    ///
    /// "My plugins are not loading" has four causes, and from inside the game three of them look
    /// exactly the same - the mod is simply not there:
    ///
    ///   * in Plugins\LSPDFR and loaded       - nothing to say
    ///   * in Plugins\LSPDFR and not loaded   - LSPDFR logs it and carries on to the next one
    ///   * in Plugins\                        - RPH loads it into its own AppDomain, where it cannot
    ///                                          see LSPDFR at all, so it runs and does nothing
    ///   * in lspdfr\                         - LSPDFR's data folder, which is never scanned for
    ///                                          plugins, so the DLL is never even looked at
    ///
    /// The last two are the ones that cost an evening: the file is installed, the game starts, and
    /// the mod is absent with no error anywhere. This says which of the four happened, by name.
    ///
    /// Nothing here loads anything. Identities and references are read straight out of the files as
    /// metadata, so a broken plugin cannot break the thing that is diagnosing it.
    /// </summary>
    internal sealed class PluginInventory
    {
        private string _folder = "";
        private string _root = "";

        public readonly List<PluginEntry> Entries = new List<PluginEntry>();

        /// <summary>Plugins that are installed somewhere LSPDFR will never look, and where.</summary>
        public readonly List<string> Misplaced = new List<string>();

        /// <summary>False when LSPDFR could not be asked, so only the AppDomain was consulted.</summary>
        public bool LspdfrListedItsPlugins;

        public int PluginsFound
        {
            get
            {
                int count = 0;
                foreach (var entry in Entries) if (entry.IsPlugin) count++;
                return count;
            }
        }

        public int PluginsLoaded
        {
            get
            {
                int count = 0;
                foreach (var entry in Entries) if (entry.IsPlugin && entry.IsLoaded) count++;
                return count;
            }
        }

        public List<PluginEntry> NotLoaded
        {
            get
            {
                var missing = new List<PluginEntry>();
                foreach (var entry in Entries) if (entry.IsPlugin && !entry.IsLoaded) missing.Add(entry);
                return missing;
            }
        }

        public static PluginInventory Scan(LspdfrApi api)
        {
            var inventory = new PluginInventory();
            try { inventory.Run(api); }
            catch (Exception ex) { Log.Error("plugin inventory", ex); }
            return inventory;
        }

        // ------------------------------------------------------------------ the scan

        private void Run(LspdfrApi api)
        {
            _folder = Settings.PluginFolder();
            _root = GameRoot(_folder);

            // Who LSPDFR says it loaded. This is the authoritative list; the AppDomain is the
            // fallback for builds that do not expose one.
            var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var assemblies = api == null ? null : api.UserPlugins();
            if (assemblies != null)
            {
                LspdfrListedItsPlugins = true;
                foreach (var assembly in assemblies)
                {
                    try { registered.Add(assembly.GetName().Name); }
                    catch { }
                }
            }

            var inDomain = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { inDomain.Add(assembly.GetName().Name); }
                catch { }
            }

            var resolvable = ResolvableNames(inDomain);

            ReadFolder(_folder, registered, inDomain, resolvable);
            FindMisplaced(Path.Combine(_root, "Plugins"), "Plugins", inDomain);
            FindMisplaced(Path.Combine(_root, "lspdfr"), "lspdfr", inDomain);
        }

        /// <summary>Every DLL in the plugin folder, and what became of it.</summary>
        private void ReadFolder(string folder, HashSet<string> registered, HashSet<string> inDomain,
                                HashSet<string> resolvable)
        {
            if (!Directory.Exists(folder)) return;

            string[] files;
            try { files = Directory.GetFiles(folder, "*.dll"); }
            catch (Exception ex) { Log.Error("reading " + folder, ex); return; }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (var path in files)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name)) continue;

                var entry = new PluginEntry { Name = name };
                Entries.Add(entry);

                var identity = Identity(path);
                entry.Version = identity == null || identity.Version == null ? null : identity.Version.ToString();

                // What LSPDFR and the AppDomain report is the assembly's own name, which is not
                // always the file's - a pack can ship a DLL under any name it likes. Match on the
                // one the runtime uses, and fall back to the file name if it cannot be read.
                var assemblyName = identity == null || string.IsNullOrEmpty(identity.Name) ? name : identity.Name;

                var references = References(path);
                if (references == null)
                {
                    entry.State = PluginState.Unreadable;
                    continue;
                }

                if (!RefersToLspdfr(references))
                {
                    // A dependency someone dropped in beside a pack - RAGENativeUI, LiteDB, one of
                    // the obfuscated loader stubs. It is not something LSPDFR is meant to load.
                    entry.State = PluginState.Library;
                    continue;
                }

                // A pack built with Costura carries copies of its dependencies inside itself, and
                // the resources say which. Those are present, just not as files - reporting them as
                // missing would be wrong, and would bury the ones that really are.
                var embedded = EmbeddedNames(path);

                foreach (var reference in references)
                {
                    var needed = reference.Name;
                    if (string.IsNullOrEmpty(needed)) continue;
                    if (IsAlwaysPresent(needed)) continue;
                    if (embedded.Contains(needed)) { Add(entry.Merged, needed); continue; }
                    if (IsObfuscated(needed)) { Add(entry.Merged, needed); continue; }
                    if (resolvable.Contains(needed)) continue;

                    // Two references to the same assembly at different versions - the same pack built
                    // against two releases of something - are one thing missing, not two.
                    Add(entry.Missing, needed);
                }

                if (registered.Contains(assemblyName)) entry.State = PluginState.Loaded;
                else if (inDomain.Contains(assemblyName)) entry.State = PluginState.InDomain;
                else entry.State = PluginState.NotLoaded;
            }
        }

        /// <summary>
        /// DLLs that are LSPDFR plugins but are not in the folder LSPDFR reads.
        ///
        /// This is the failure that leaves no trace: LSPDFR scans Plugins\LSPDFR and nothing else, so
        /// a plugin three folders away is never opened, and where RPH picks one up from Plugins\ it
        /// gets its own AppDomain and cannot see LSPDFR's types.
        /// </summary>
        private void FindMisplaced(string folder, string label, HashSet<string> inDomain)
        {
            if (!Directory.Exists(folder)) return;
            if (string.Equals(folder, _folder, StringComparison.OrdinalIgnoreCase)) return;

            string[] files;
            try { files = Directory.GetFiles(folder, "*.dll"); }
            catch { return; }

            foreach (var path in files)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name)) continue;

                // LSPDFR itself lives in Plugins\, which is where it belongs.
                if (name.IndexOf("LSPD First Response", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (name.IndexOf("RagePluginHook", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                var references = References(path);
                if (references == null || !RefersToLspdfr(references)) continue;

                var identity = Identity(path);
                var assemblyName = identity == null || string.IsNullOrEmpty(identity.Name) ? name : identity.Name;
                if (inDomain.Contains(assemblyName)) continue;      // loaded from somewhere, and working
                Misplaced.Add(name + " (in " + label + "\\)");
            }
        }

        // ------------------------------------------------------------------ reading assemblies

        /// <summary>
        /// The assembly's identity, without loading it into the process. Reading the name and version
        /// out of the file is a metadata read; a plugin that cannot be loaded can still be described.
        /// </summary>
        private static AssemblyName Identity(string path)
        {
            try { return AssemblyName.GetAssemblyName(path); }
            catch { return null; }
        }

        /// <summary>
        /// What the assembly says it depends on.
        ///
        /// Reflection-only, which reads the dependency table without resolving any of it - so this
        /// works for a plugin whose dependencies are missing, which is exactly the case worth
        /// reporting.
        /// </summary>
        private static AssemblyName[] References(string path)
        {
            try { return Assembly.ReflectionOnlyLoadFrom(path).GetReferencedAssemblies(); }
            catch { return null; }
        }

        /// <summary>
        /// The dependencies a pack has merged into itself, read from its embedded resources.
        ///
        /// Costura - the usual way a C# mod ships its dependencies - stores each one as
        /// "costura.&lt;name&gt;.dll.compressed" or ".zip", and resolves the reference out of that resource
        /// at runtime. So a reference to something with a matching resource is not missing at all.
        /// CompuLite is built this way, which is why its LiteDB reference is not a problem.
        /// </summary>
        private static HashSet<string> EmbeddedNames(string path)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var resource in Assembly.ReflectionOnlyLoadFrom(path).GetManifestResourceNames())
                {
                    if (resource == null) continue;
                    if (!resource.StartsWith("costura.", StringComparison.OrdinalIgnoreCase)) continue;

                    var rest = resource.Substring("costura.".Length);
                    var dll = rest.IndexOf(".dll", StringComparison.OrdinalIgnoreCase);
                    if (dll <= 0) continue;

                    names.Add(rest.Substring(0, dll));
                }
            }
            catch { }
            return names;
        }

        private static bool RefersToLspdfr(AssemblyName[] references)
        {
            foreach (var reference in references)
            {
                if (reference.Name == null) continue;
                if (reference.Name.IndexOf("LSPD First Response", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Everything a reference could legitimately resolve to: what is already loaded in this
        /// process, and every DLL in the folders the loader probes.
        /// </summary>
        private static HashSet<string> ResolvableNames(HashSet<string> inDomain)
        {
            var names = new HashSet<string>(inDomain, StringComparer.OrdinalIgnoreCase);

            AddFolder(names, Settings.PluginFolder());
            AddFolder(names, Path.Combine(GameRoot(Settings.PluginFolder()), "Plugins"));
            AddFolder(names, GameRoot(Settings.PluginFolder()));
            // LSPDFR's data folder, which is where people drop the odd managed DLL by hand.
            AddFolder(names, Path.Combine(GameRoot(Settings.PluginFolder()), "lspdfr"));

            return names;
        }

        private static void AddFolder(HashSet<string> names, string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.GetFiles(folder, "*.dll"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(name)) names.Add(name);
                }
            }
            catch { }
        }

        /// <summary>
        /// Plugins\LSPDFR -> Plugins -> the game folder, which is what the assembly loader treats as
        /// the application base. Validated, because PluginFolder() falls back to the working
        /// directory when the plugin's own location cannot be read.
        /// </summary>
        private static string GameRoot(string pluginFolder)
        {
            try
            {
                var plugins = Path.GetDirectoryName(pluginFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrEmpty(plugins))
                {
                    var root = Path.GetDirectoryName(plugins.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (!string.IsNullOrEmpty(root) &&
                        Directory.Exists(Path.Combine(root, "Plugins")) &&
                        string.Equals(Path.GetFileName(plugins), "Plugins", StringComparison.OrdinalIgnoreCase))
                        return root;
                }
            }
            catch { }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static void Add(List<string> names, string name)
        {
            foreach (var existing in names)
                if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) return;
            names.Add(name);
        }

        /// <summary>
        /// Present by definition, whatever the folders on this machine hold.
        ///
        /// .NET itself, kept to prefixes rather than a list of assembly names that would go stale.
        /// And the two assemblies this code cannot be running without: LSPDFR loaded this plugin, and
        /// RAGE Plugin Hook is the host. A plugin built against an older release of either still
        /// binds, because neither is strong-named - only the simple name is matched - so a version
        /// difference is not something to report.
        /// </summary>
        private static bool IsAlwaysPresent(string name)
        {
            if (string.Equals(name, "mscorlib", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(name, "netstandard", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(name, "WindowsBase", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(name, "RagePluginHook", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.IndexOf("LSPD First Response", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.StartsWith("System", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// A name no human wrote. ConfuserEx encrypts the dependencies it merges in and renames the
        /// references to random strings - "hOleASPuLRzxElnMbwNUCLwGAShkA" - which resolve out of
        /// the pack's own resources at runtime.
        ///
        /// Long and capital-heavy together, rather than length alone: "ExternalPoliceComputer" is
        /// also twenty-odd characters with no dot in it, and it is a real product name that really is
        /// missing when it is not installed. Judging by length alone would have hidden it.
        /// </summary>
        private static bool IsObfuscated(string name)
        {
            if (name.Length < 20 || name.IndexOf('.') >= 0) return false;

            int capitals = 0;
            foreach (var character in name) if (char.IsUpper(character)) capitals++;

            return capitals >= 6 && capitals * 5 >= name.Length;
        }

        // ------------------------------------------------------------------ reporting

        /// <summary>One line, for when the box opens.</summary>
        public string Headline()
        {
            if (PluginsFound == 0)
                return "Plugins: none found in " + _folder + ". If that folder should have plugins in it, this plugin is in the wrong place.";

            var notLoaded = NotLoaded;
            if (notLoaded.Count == 0)
                return "Plugins: all " + PluginsFound + " loaded.";

            var names = new List<string>();
            foreach (var entry in notLoaded) names.Add(entry.Name);

            return "Plugins: " + PluginsLoaded + " of " + PluginsFound + " loaded. Not loaded: " +
                   string.Join(", ", names.ToArray()) + ".";
        }

        /// <summary>
        /// The things worth interrupting somebody for: something installed where LSPDFR will never
        /// look, or a pack reaching for something that is not on the machine. The second is how a
        /// plugin that loaded perfectly well takes the whole game down an hour later.
        /// </summary>
        public List<string> Warnings()
        {
            var warnings = new List<string>();

            var notLoaded = NotLoaded;
            if (notLoaded.Count > 0)
            {
                var names = new List<string>();
                foreach (var entry in notLoaded) names.Add(entry.Name);
                warnings.Add("Did not load: " + string.Join(", ", names.ToArray()) +
                             ". LSPDFR creates a plugin from every DLL in this folder and these are not among them - " +
                             "RagePluginHook.log says why, beside each one's own 'Creating plugin:' line (or its absence).");
            }

            foreach (var name in Misplaced)
                warnings.Add("Wrong folder: " + name + ". LSPDFR only ever scans Plugins\\LSPDFR, so this one never runs - move it there.");

            // A reference to something absent is a *hint*, not a verdict. In practice these are almost
            // always optional integrations with another pack - StopThePed's BetterEMS, a callout pack's
            // Callout Interface link - which switch one feature off and carry on. Warning that they
            // "crash the game" trains the reader to ignore the line, and then the one time it matters
            // they will ignore it too. So the two cases are worded differently: a plugin that failed to
            // load alongside a missing dependency is the likely cause, and that is worth saying plainly.
            var optional = new List<string>();
            var fatal = new List<string>();

            foreach (var entry in Entries)
            {
                if (!entry.IsPlugin || entry.Missing.Count == 0) continue;
                var line = entry.Name + " -> " + string.Join(", ", entry.Missing.ToArray());
                if (entry.IsLoaded) optional.Add(line);
                else fatal.Add(line);
            }

            if (fatal.Count > 0)
                warnings.Add("Missing dependency: " + string.Join("; ", fatal.ToArray()) +
                             " - and those plugins did not load, so this is very probably why.");

            if (optional.Count > 0)
                warnings.Add("Optional integrations not installed: " + string.Join("; ", optional.ToArray()) +
                             ".  Those plugins loaded without them; the features they add are simply switched off. " +
                             "/plugins lists them and the log has the detail.");

            return warnings;
        }

        /// <summary>The full list, for /plugins and tdplugins.</summary>
        public List<string> Detail()
        {
            var lines = new List<string>();
            lines.Add("Plugins in " + _folder + ": " + PluginsLoaded + " of " + PluginsFound + " loaded" +
                      (LspdfrListedItsPlugins ? "." : " (LSPDFR would not list them; the AppDomain was used)."));

            foreach (var entry in Entries)
            {
                if (!entry.IsPlugin) continue;
                lines.Add("  " + entry.Name + (string.IsNullOrEmpty(entry.Version) ? "" : " " + entry.Version) +
                          "  -  " + entry.StateWord);
            }

            foreach (var entry in Entries)
            {
                if (entry.Missing.Count == 0) continue;
                lines.Add("  " + entry.Name + " refers to: " + string.Join(", ", entry.Missing.ToArray()) +
                          (entry.IsLoaded ? " (not installed; optional integration, feature off)"
                                          : " (not installed, and this plugin did not load)"));
            }

            foreach (var name in Misplaced)
                lines.Add("  Wrong folder: " + name);

            return lines;
        }

        /// <summary>Everything, to the log, which is the copy that survives the game being closed.</summary>
        public void WriteToLog()
        {
            Log.Line("inventory: " + Headline());
            if (!LspdfrListedItsPlugins)
                Log.Line("inventory: LSPDFR could not list its plugins, so the AppDomain was used instead");

            foreach (var entry in Entries)
            {
                Log.Line("inventory:   " + entry.Name +
                         (string.IsNullOrEmpty(entry.Version) ? "" : " " + entry.Version) +
                         " - " + entry.StateWord);

                foreach (var needed in entry.Missing)
                    Log.Line("inventory:       references " + needed + ", which is not installed - " +
                             (entry.IsLoaded
                                 ? "an optional integration; the feature that uses it is switched off"
                                 : "and this plugin did not load, so this is probably why"));

                foreach (var merged in entry.Merged)
                    Log.Line("inventory:       refers to " + merged + " (obfuscated or merged in; ignored)");
            }

            foreach (var name in Misplaced)
                Log.Line("inventory:   misplaced: " + name + " - LSPDFR only scans Plugins\\LSPDFR");
        }
    }
}
