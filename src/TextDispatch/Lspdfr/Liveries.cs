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

        // body -> the cars that look the same, the base car first; and each of those cars -> its body.
        private readonly Dictionary<string, List<string>> _bodies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _bodyOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Cars whose whole body texture is one department's design - the bus copies, lvpolicet1 ... - so they
        // have no liveries to change: bringing one is all it takes. [skins] says car = department.
        private readonly HashSet<string> _skins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    else if (section.Equals("bodies", StringComparison.OrdinalIgnoreCase))
                    {
                        var cars = new List<string>();
                        foreach (var part in value.Split(','))
                        {
                            var car = part.Trim();
                            if (car.Length == 0 || cars.Contains(car)) continue;
                            cars.Add(car);
                            map._bodyOf[car] = key;
                            map._modelByHash[Joaat(car)] = car;
                        }
                        if (cars.Count > 0) map._bodies[key] = cars;
                    }
                    else if (section.Equals("skins", StringComparison.OrdinalIgnoreCase))
                    {
                        // The car is painted as this department through and through: livery 1 of 1, for the
                        // lookups, and never repainted.
                        Dictionary<string, int> car;
                        if (!map._cars.TryGetValue(key, out car))
                        {
                            car = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            map._cars[key] = car;
                            map._models.Add(key);
                            map._modelByHash[Joaat(key)] = key;
                        }
                        car[value] = 1;
                        map._skins.Add(key);
                        if (!map._names.ContainsKey(value)) { map._departments.Add(value); map._names[value] = value; }
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
                         " department(s), " + map._bodies.Count + " bodies, " + map._skins.Count + " skin(s) from " + location);
            }
            catch (Exception ex) { Log.Error("liveries", ex); }
            return map;
        }

        /// <summary>The cars the file knows: the base cars and the cars that share their bodies first, then the copies.</summary>
        public IList<string> Models
        {
            get
            {
                var list = new List<string>();
                foreach (var body in _bodies.Values)
                    foreach (var car in body)
                        if (!_cars.ContainsKey(car) && !list.Contains(car)) list.Add(car);
                foreach (var car in _models)
                    if (!list.Contains(car)) list.Add(car);
                return list;
            }
        }

        /// <summary>Whether this car is one department's design all over - a [skins] car, with no liveries to change.</summary>
        public bool IsSkin(string model)
        {
            return model != null && _skins.Contains(model);
        }

        /// <summary>Every department the file names, in its own order.</summary>
        public IList<string> Departments { get { return _departments; } }

        /// <summary>Whether the file names this department, by its key.</summary>
        public bool Knows(string department)
        {
            return department != null && _names.ContainsKey(department);
        }

        /// <summary>
        /// Whether a department is a real city's design - LIVERIES - REAL, "LSPD - Atlanta style" - rather
        /// than one of the pack's own San Andreas departments, lspd, bcso, sahp.
        /// </summary>
        public bool IsReal(string department)
        {
            return department != null && NameOf(department).EndsWith("style", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The liveries on this car that are no real city's: the ones it came with - numbered below the
        /// first department - and the San Andreas departments. Empty for a car the file does not number,
        /// and for a copy that carries only real cities.
        /// </summary>
        public List<int> PlainLiveries(string model)
        {
            var list = new List<int>();
            var liveries = LiveriesOf(model);
            if (liveries.Count == 0) return list;

            if (IsSkin(model))
            {
                if (!IsReal(liveries[0].Key)) list.Add(1);
                return list;
            }

            for (int number = 1; number < liveries[0].Value; number++) list.Add(number);
            foreach (var pair in liveries)
                if (!IsReal(pair.Key)) list.Add(pair.Value);
            return list;
        }

        /// <summary>The car with this body that has a livery no real city's: the one named first, then the others in order.</summary>
        public string PlainCarrier(string model)
        {
            foreach (var car in ModelsFor(model))
                if (PlainLiveries(car).Count > 0) return car;
            return null;
        }

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

        /// <summary>Whether any car with this body has liveries on file.</summary>
        public bool HasCar(string model)
        {
            foreach (var car in ModelsFor(model))
                if (_cars.ContainsKey(car)) return true;
            return false;
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
            return Pick(new List<string>(DepartmentsFor(model)), typed, out candidates);
        }

        /// <summary>
        /// The same, across every car with this body - the base car and its copies - and which of them
        /// carries it: the car named, when it does, or else the first copy that does.
        /// </summary>
        public string ResolveAcross(string model, string typed, out string carrier, out List<string> candidates)
        {
            carrier = null;
            var department = Pick(new List<string>(DepartmentsAcross(model)), typed, out candidates);
            if (department != null) carrier = Carrier(model, department);
            return department;
        }

        /// <summary>The cars that look like this one, itself first: a body's base car and its copies.</summary>
        public List<string> ModelsFor(string model)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(model)) return list;

            List<string> body;
            string name;
            if (_bodies.TryGetValue(model, out body) ||
                (_bodyOf.TryGetValue(model, out name) && _bodies.TryGetValue(name, out body)))
            {
                if (!_bodies.ContainsKey(model) || body.Contains(model)) list.Add(model);
                foreach (var car in body)
                    if (!list.Contains(car)) list.Add(car);
            }
            else list.Add(model);
            return list;
        }

        /// <summary>Every department any car with this body carries.</summary>
        public IEnumerable<string> DepartmentsAcross(string model)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var car in ModelsFor(model))
                foreach (var key in DepartmentsFor(car))
                    if (seen.Add(key)) yield return key;
        }

        /// <summary>The car with this body that carries a department: the one named first, then its copies in order.</summary>
        public string Carrier(string model, string department)
        {
            foreach (var car in ModelsFor(model))
                if (NumberFor(car, department) > 0) return car;
            return null;
        }

        /// <summary>
        /// Whether this is one of the file's cars, as opposed to a body's name - "stanier" and "bison" are
        /// also the civilian cars, which must stay civilian unless a department is asked for.
        /// </summary>
        public bool IsCar(string model)
        {
            return model != null && (_cars.ContainsKey(model) || _bodyOf.ContainsKey(model));
        }

        /// <summary>The name of the body a car has - "interceptor" for police3 and its copies - or null.</summary>
        public string BodyOf(string model)
        {
            string body;
            if (model == null) return null;
            if (_bodies.ContainsKey(model)) return model;
            return _bodyOf.TryGetValue(model, out body) ? body : null;
        }

        /// <summary>
        /// Every livery any car with this body carries, once each: (department, the car that carries it,
        /// its number there) - the copies in order, then each copy's liveries by number.
        /// </summary>
        public List<Tuple<string, string, int>> AllLiveries(string model)
        {
            var list = new List<Tuple<string, string, int>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var car in ModelsFor(model))
                foreach (var pair in LiveriesOf(car))
                    if (seen.Add(pair.Key)) list.Add(Tuple.Create(pair.Key, car, pair.Value));
            return list;
        }

        /// <summary>The base car of a copy - police3 for lvinterceptor2 - or the car itself.</summary>
        public string BaseOf(string model)
        {
            string body;
            List<string> cars;
            if (model != null && _bodyOf.TryGetValue(model, out body) && _bodies.TryGetValue(body, out cars)) return cars[0];
            return model;
        }

        /// <summary>The same, across every department on file, whatever car carries it: "/veh atlanta".</summary>
        public string ResolveAny(string typed, out List<string> candidates)
        {
            return Pick(new List<string>(_departments), typed, out candidates);
        }

        // The cars a department is brought in when no car is named, best first. An old design goes on the
        // Stanier, the car of that era; anything else on the Interceptor. Bodies the file has that are not
        // here come after, in the file's order, and then the cars of no body - riot, polmav ...
        private static readonly string[] Preferred =
            { "interceptor", "stanier", "granger", "gresley", "fugitive", "landstalker", "contender", "bison", "buffalo", "firetruk", "ambulance", "policet" };

        /// <summary>The car to bring a department in when only the department is named, or null when no car carries it.</summary>
        public string CarFor(string department)
        {
            if (department == null) return null;

            var order = new List<string>();
            if (department.EndsWith("-old", StringComparison.OrdinalIgnoreCase)) order.Add("stanier");
            foreach (var body in Preferred) if (_bodies.ContainsKey(body) && !order.Contains(body)) order.Add(body);
            foreach (var body in _bodies.Keys) if (!order.Contains(body)) order.Add(body);

            foreach (var body in order)
            {
                var car = Carrier(body, department);
                if (car != null) return car;
            }
            foreach (var car in _models)
                if (!_bodyOf.ContainsKey(car) && NumberFor(car, department) > 0) return car;
            return null;
        }

        // Words that say which kind of department, not where: "atlanta fire", "fulton sheriff", "texas state".
        private static readonly Dictionary<string, string> Kinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "police", "police" }, { "pd", "police" }, { "cops", "police" },
            { "sheriff", "sheriff" }, { "sheriffs", "sheriff" }, { "so", "sheriff" },
            { "state", "state" }, { "trooper", "state" }, { "troopers", "state" }, { "highway", "state" },
            { "fire", "fire" }, { "fd", "fire" }, { "ems", "fire" }, { "rescue", "fire" },
        };

        private static readonly HashSet<string> Olds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "old", "older", "classic", "retro" };

        // Said, but saying nothing a department's words would not: "texas highway patrol" is sahp-texas.
        private static readonly HashSet<string> Fillers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "patrol", "department", "dept", "office", "of", "the", "style", "livery" };

        // What people call a place, for the words the file spells it with.
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "nyc", "new york city" }, { "la", "los angeles" }, { "sf", "san francisco" }, { "philly", "philadelphia" },
            { "okc", "oklahoma city" }, { "kc", "kansas city" }, { "nola", "new orleans" }, { "slc", "salt lake" },
            { "vegas", "las vegas" }, { "co", "county" }, { "cnty", "county" }, { "ft", "fort" }, { "saint", "st" },
            // And the agencies people know by their letters.
            { "nypd", "new york city" }, { "lapd", "los angeles" }, { "lasd", "los angeles county" }, { "lvmpd", "las vegas" },
            { "sfpd", "san francisco" }, { "sdpd", "san diego" }, { "mdpd", "miami dade" }, { "chp", "california" },
            { "fhp", "florida" }, { "nysp", "new york" }, { "njsp", "new jersey" },
        };

        /// <summary>
        /// What somebody meant by a department, out of these: the key or the full name exactly, or else
        /// the one whose words take in every word typed - "atlanta" for lspd-atlanta, "fulton" for
        /// lssd-fulton-county, "atlanta old" or "atlanta fire" for the others with Atlanta in them.
        /// When more than one fits, the current design beats the old one unless "old" was typed; whole
        /// words beat begun ones ("atl"); the one with nothing left over beats a longer one ("dallas" is
        /// the city, "dallas county" the county); and police beat sheriffs, the state and fire, unless
        /// one of those was named. `candidates` holds what is still level, so the box can list them.
        /// </summary>
        private string Pick(List<string> mine, string typed, out List<string> candidates)
        {
            candidates = new List<string>();
            var text = (typed ?? "").Trim();
            if (text.Length == 0) return null;

            foreach (var key in mine)
                if (key.Equals(text, StringComparison.OrdinalIgnoreCase) ||
                    NameOf(key).Equals(text, StringComparison.OrdinalIgnoreCase))
                    return key;

            bool old = false;
            string kind = null;
            var place = new List<string>();
            var said = new List<string>();
            foreach (var word in Words(text))
            {
                string alias;
                if (Olds.Contains(word)) old = true;
                else if (Kinds.ContainsKey(word)) { if (kind == null) kind = Kinds[word]; said.Add(word); }
                else if (Fillers.Contains(word)) { }
                else if (Aliases.TryGetValue(word, out alias)) place.AddRange(Words(alias));
                else place.Add(word);
            }
            if (place.Count == 0 && kind == null && !old) return null;

            // Only a kind - "highway patrol", "sheriff" - is first the words of a name, sahp's or lssd's,
            // and then any department of that kind.
            var fits = Fits(mine, place.Count > 0 ? place : said, old, kind);
            if (fits.Count == 0 && place.Count == 0 && said.Count > 0) fits = Fits(mine, place, old, kind);
            if (fits.Count == 0) return null;

            if (!old && fits.Exists(f => !f.Item1.EndsWith("-old", StringComparison.OrdinalIgnoreCase)))
                fits.RemoveAll(f => f.Item1.EndsWith("-old", StringComparison.OrdinalIgnoreCase));
            int fewest = int.MaxValue;
            foreach (var f in fits) fewest = Math.Min(fewest, f.Item2);
            fits.RemoveAll(f => f.Item2 > fewest);
            fewest = int.MaxValue;
            foreach (var f in fits) fewest = Math.Min(fewest, f.Item3);
            fits.RemoveAll(f => f.Item3 > fewest);
            if (kind == null)
            {
                int best = int.MaxValue;
                foreach (var f in fits) best = Math.Min(best, Rank(KindOf(f.Item1)));
                fits.RemoveAll(f => Rank(KindOf(f.Item1)) > best);
            }

            foreach (var f in fits) candidates.Add(f.Item1);
            return candidates.Count == 1 ? candidates[0] : null;
        }

        /// <summary>
        /// Every department that takes in each of the words, as (key, words only begun, place words in the
        /// key nobody typed) - "atl" is begun; "county" in lssd-dallas-county is left over, for "dallas".
        /// </summary>
        private List<Tuple<string, int, int>> Fits(List<string> mine, List<string> place, bool old, string kind)
        {
            var fits = new List<Tuple<string, int, int>>();
            foreach (var key in mine)
            {
                bool isOld = key.EndsWith("-old", StringComparison.OrdinalIgnoreCase);
                if (old && !isOld) continue;
                if (kind != null && KindOf(key) != kind) continue;

                var words = WordsOf(key);
                int begun = 0;
                bool all = true;
                foreach (var word in place)
                {
                    if (words.Contains(word)) continue;
                    bool starts = false;
                    foreach (var own in words)
                        if (Begins(own, word)) { starts = true; break; }
                    if (!starts) { all = false; break; }
                    begun++;
                }
                if (!all) continue;

                int over = 0;
                var parts = key.ToLowerInvariant().Split('-');
                for (int i = 1; i < parts.Length - (isOld ? 1 : 0); i++)
                {
                    bool covered = false;
                    foreach (var word in place)
                        if (word == parts[i] || Begins(parts[i], word)) { covered = true; break; }
                    if (!covered) over++;
                }
                fits.Add(Tuple.Create(key, begun, over));
            }
            return fits;
        }

        // A department's words, key and full name together - kept, since every /veh looks at all of them.
        private readonly Dictionary<string, HashSet<string>> _wordsOf = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> WordsOf(string key)
        {
            HashSet<string> words;
            if (!_wordsOf.TryGetValue(key, out words))
            {
                words = new HashSet<string>(Words(key + " " + NameOf(key)));
                _wordsOf[key] = words;
            }
            return words;
        }

        // What /veh help shows, worked out once per reading of the file.
        private List<Tuple<string, string, string, bool>> _vehNames;

        private static readonly string[] Groups = { "San Andreas", "City police", "County police", "Sheriffs", "State", "Fire" };

        /// <summary>
        /// Every department as you would type it after /veh, for /veh help: (group, what to type, key, whether
        /// it has an old design too), by group and then alphabetical. What to type is the first of the place
        /// ("atlanta"), the place and its kind ("atlanta fire") and the key that ResolveAny takes back to that
        /// department. An old design is folded into its current one, and stands alone only when there is none.
        /// </summary>
        public List<Tuple<string, string, string, bool>> VehNames()
        {
            if (_vehNames != null) return _vehNames;

            var list = new List<Tuple<string, string, string, bool>>();
            foreach (var key in _departments)
            {
                bool isOld = key.EndsWith("-old", StringComparison.OrdinalIgnoreCase);
                var current = isOld ? key.Substring(0, key.Length - 4) : key;
                if (isOld && _names.ContainsKey(current)) continue;

                var group = GroupOf(key);
                var typed = key;
                if (group != Groups[0])
                {
                    var parts = current.ToLowerInvariant().Split('-');
                    var place = string.Join(" ", parts, 1, parts.Length - 1);
                    foreach (var each in new[] { place, place + " " + KindOf(key) })
                    {
                        var word = isOld ? each + "-old" : each;
                        List<string> candidates;
                        if (key.Equals(ResolveAny(word, out candidates), StringComparison.OrdinalIgnoreCase)) { typed = word; break; }
                    }
                }
                list.Add(Tuple.Create(group, typed, key, !isOld && _names.ContainsKey(key + "-old")));
            }

            list.Sort((a, b) =>
            {
                int byGroup = Array.IndexOf(Groups, a.Item1).CompareTo(Array.IndexOf(Groups, b.Item1));
                return byGroup != 0 ? byGroup : string.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase);
            });
            _vehNames = list;
            return list;
        }

        // The game's own departments - lspd, sahp, lspd-bw - are a group of their own, and go by their keys;
        // the real-world styles by what they are.
        private string GroupOf(string key)
        {
            if (key.IndexOf('-') < 0 || !NameOf(key).EndsWith("style", StringComparison.OrdinalIgnoreCase)) return Groups[0];
            if (key.StartsWith("lscpd-", StringComparison.OrdinalIgnoreCase)) return "County police";
            switch (KindOf(key))
            {
                case "sheriff": return "Sheriffs";
                case "state": return "State";
                case "fire": return "Fire";
                default: return "City police";
            }
        }

        // A word begun counts once it is three letters, so "at" is not Atlanta and Athens both.
        private static bool Begins(string whole, string start)
        {
            return start.Length >= 3 && whole.StartsWith(start, StringComparison.Ordinal);
        }

        /// <summary>A department's kind, by its key's first part: lsfd is fire, lssd and bcso sheriffs, sahp and sasp the state.</summary>
        private static string KindOf(string key)
        {
            var prefix = key.Split('-')[0].ToLowerInvariant();
            if (prefix.EndsWith("fd")) return "fire";
            if (prefix.EndsWith("sd") || prefix.EndsWith("so")) return "sheriff";
            if (prefix.EndsWith("hp") || prefix.EndsWith("sp")) return "state";
            return "police";
        }

        private static int Rank(string kind)
        {
            switch (kind) { case "police": return 0; case "sheriff": return 1; case "state": return 2; default: return 3; }
        }

        /// <summary>Lower-case words, apostrophes dropped so "Prince George's" is "prince georges".</summary>
        private static IEnumerable<string> Words(string text)
        {
            var word = new System.Text.StringBuilder();
            foreach (var c in (text ?? "").ToLowerInvariant() + " ")
            {
                if (char.IsLetterOrDigit(c)) word.Append(c);
                else if (c == '\'' || c == '’') continue;
                else if (word.Length > 0) { yield return word.ToString(); word.Length = 0; }
            }
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
