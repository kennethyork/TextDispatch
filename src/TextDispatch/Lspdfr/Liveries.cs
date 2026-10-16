using System;
using System.Collections.Generic;
using System.IO;
using Rage;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// Which livery on which car belongs to which department: TextDispatch.liveries.ini, written by the
    /// pack's LIVERY GENERATOR\build_livery_slots.py.
    ///
    /// A car's liveries are numbered textures in its .ytd - stanier_sign_1, stanier_sign_2 ... - and the
    /// game knows them only by number. This file is what puts a department on each number, so that
    /// "/veh police3 sahp" can mean something. It is read again whenever it changes on disk, so a rebuild
    /// of the liveries needs no restart of the game.
    /// </summary>
    internal sealed class LiveryMap
    {
        public const string FileName = "TextDispatch.liveries.ini";

        private static LiveryMap _current;
        private static DateTime _stamp;

        private readonly List<string> _departments = new List<string>();
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _agencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // model -> department -> livery number, counted from 1 like the textures.
        private readonly Dictionary<string, Dictionary<string, int>> _cars =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _models = new List<string>();
        private readonly Dictionary<uint, string> _modelByHash = new Dictionary<uint, string>();

        /// <summary>Where the file is looked for, whether or not it is there.</summary>
        public string Location { get; private set; }

        public bool Found { get; private set; }

        /// <summary>The map as the file says now - read again only when the file has changed.</summary>
        public static LiveryMap Current()
        {
            var location = Path.Combine(Settings.PluginFolder(), FileName);
            var stamp = DateTime.MinValue;
            try { if (File.Exists(location)) stamp = File.GetLastWriteTimeUtc(location); }
            catch { }

            if (_current == null || stamp != _stamp ||
                !string.Equals(_current.Location, location, StringComparison.OrdinalIgnoreCase))
            {
                _current = Load(location);
                _stamp = stamp;
            }
            return _current;
        }

        private static LiveryMap Load(string location)
        {
            var map = new LiveryMap { Location = location };
            try
            {
                if (!File.Exists(location)) return map;

                string section = null;
                foreach (var raw in File.ReadAllLines(location))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                    if (line[0] == '[' && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    var split = line.IndexOf('=');
                    if (split <= 0 || section == null) continue;
                    var key = line.Substring(0, split).Trim();
                    var value = line.Substring(split + 1).Trim();
                    if (key.Length == 0 || value.Length == 0) continue;

                    if (section.Equals("departments", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!map._names.ContainsKey(key)) map._departments.Add(key);
                        map._names[key] = value;
                    }
                    else if (section.Equals("agencies", StringComparison.OrdinalIgnoreCase))
                    {
                        map._agencies[key] = value;
                    }
                    else
                    {
                        int number;
                        if (!int.TryParse(value, out number) || number < 1) continue;

                        Dictionary<string, int> car;
                        if (!map._cars.TryGetValue(section, out car))
                        {
                            car = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            map._cars[section] = car;
                            map._models.Add(section);
                            map._modelByHash[Joaat(section)] = section;
                        }
                        car[key] = number;
                        if (!map._names.ContainsKey(key)) { map._departments.Add(key); map._names[key] = key; }
                    }
                }

                map.Found = true;
                Log.Line("liveries: " + map._models.Count + " car(s), " + map._departments.Count +
                         " department(s) from " + location);
            }
            catch (Exception ex) { Log.Error("liveries", ex); }
            return map;
        }

        /// <summary>The cars the file has liveries for.</summary>
        public IList<string> Models { get { return _models; } }

        /// <summary>Every department the file names, in its own order.</summary>
        public IList<string> Departments { get { return _departments; } }

        /// <summary>The model a car in the world is, by its hash - RPH has no name for an add-on model.</summary>
        public string ModelOf(Vehicle vehicle)
        {
            if (vehicle == null) return null;
            try
            {
                string model;
                return _modelByHash.TryGetValue(vehicle.Model.Hash, out model) ? model : null;
            }
            catch { return null; }
        }

        /// <summary>A car's departments with their livery numbers, lowest number first.</summary>
        public List<KeyValuePair<string, int>> LiveriesOf(string model)
        {
            var list = new List<KeyValuePair<string, int>>();
            Dictionary<string, int> car;
            if (model != null && _cars.TryGetValue(model, out car)) list.AddRange(car);
            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            return list;
        }

        public IEnumerable<string> DepartmentsFor(string model)
        {
            foreach (var pair in LiveriesOf(model)) yield return pair.Key;
        }

        public bool HasCar(string model)
        {
            return model != null && _cars.ContainsKey(model);
        }

        /// <summary>The livery number a department has on a car, or 0 when it has none there.</summary>
        public int NumberFor(string model, string department)
        {
            Dictionary<string, int> car;
            int number;
            if (model == null || department == null || !_cars.TryGetValue(model, out car)) return 0;
            return car.TryGetValue(department, out number) ? number : 0;
        }

        /// <summary>The department behind a livery number on a car, or null for the pack's own.</summary>
        public string DepartmentAt(string model, int number)
        {
            foreach (var pair in LiveriesOf(model))
                if (pair.Value == number) return pair.Key;
            return null;
        }

        public string NameOf(string department)
        {
            string name;
            return department != null && _names.TryGetValue(department, out name) ? name : department;
        }

        /// <summary>
        /// The department for an LSPDFR agency - lspd_swat paints as lspd. The [agencies] section first,
        /// then the agency's own name, then its name without the unit after the underscore.
        /// </summary>
        public string ForAgency(string agency)
        {
            if (string.IsNullOrEmpty(agency)) return null;

            string department;
            if (_agencies.TryGetValue(agency, out department)) return department;
            if (_names.ContainsKey(agency)) return agency;

            var underscore = agency.IndexOf('_');
            if (underscore > 0 && _names.ContainsKey(agency.Substring(0, underscore))) return agency.Substring(0, underscore);
            return null;
        }

        /// <summary>
        /// What somebody meant by a department on this car: the key exactly ("sahp"), the full name
        /// ("San Andreas Highway Patrol"), or the only one starting with or containing what was typed
        /// ("atlanta"). `candidates` holds the choices when more than one fits, so the box can list them.
        /// </summary>
        public string Resolve(string model, string typed, out List<string> candidates)
        {
            candidates = new List<string>();
            var text = (typed ?? "").Trim();
            if (text.Length == 0) return null;

            var mine = new List<string>(DepartmentsFor(model));
            foreach (var key in mine)
                if (key.Equals(text, StringComparison.OrdinalIgnoreCase) ||
                    NameOf(key).Equals(text, StringComparison.OrdinalIgnoreCase))
                    return key;

            foreach (var key in mine)
                if (key.StartsWith(text, StringComparison.OrdinalIgnoreCase)) candidates.Add(key);

            if (candidates.Count == 0)
                foreach (var key in mine)
                    if (key.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        NameOf(key).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                        candidates.Add(key);

            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>GTA's model hash: Jenkins one-at-a-time over the lower-case name.</summary>
        private static uint Joaat(string text)
        {
            unchecked
            {
                uint hash = 0;
                foreach (var c in text.ToLowerInvariant())
                {
                    hash += c;
                    hash += hash << 10;
                    hash ^= hash >> 6;
                }
                hash += hash << 3;
                hash ^= hash >> 11;
                hash += hash << 15;
                return hash;
            }
        }
    }
}
