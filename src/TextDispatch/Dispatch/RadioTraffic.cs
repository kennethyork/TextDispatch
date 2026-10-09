using System;
using System.Collections.Generic;

namespace TextDispatch.Dispatch
{
    /// <summary>
    /// The rest of the city on the radio: other units going on scene, clearing, running plates, asking
    /// for a unit to back them. None of it is real and none of it asks anything of the player - it is
    /// there so the radio is a city's radio, not a line between one officer and one dispatcher.
    ///
    /// Made up from a fixed set of other callsigns, districts and calls, so the same few units turn up
    /// all shift and a unit that went on scene is later heard clearing. No reference to the game.
    /// </summary>
    internal sealed class RadioTraffic
    {
        private static readonly string[] Districts =
        {
            "Vinewood Boulevard", "Del Perro Pier", "Strawberry Avenue", "Mirror Park", "Vespucci Beach",
            "Little Seoul", "La Mesa", "Rockford Hills", "Davis Avenue", "Pillbox Hill", "Textile City",
            "Burton", "Morningwood", "El Burro Heights", "Chamberlain Hills", "Banning", "Cypress Flats",
            "Rancho", "the Legion Square garage", "the Maze Bank Arena", "LSIA arrivals", "Grove Street",
            "Hawick Avenue", "the Vinewood Bowl", "Elysian Island", "the Del Perro Freeway"
        };

        private static readonly string[] Calls =
        {
            "a noise complaint", "a shoplifter in custody", "a two-car collision, no injuries", "a suspicious vehicle",
            "an alarm call", "a domestic, all quiet on arrival", "a stray dog in the road", "a welfare check",
            "a vehicle blocking a driveway", "a fight outside a bar", "a found property report", "an abandoned vehicle",
            "a trespasser", "a broken-down truck on the shoulder", "a report of shots heard, nothing found",
            "a theft from a vehicle", "a drunk and disorderly", "graffiti in progress"
        };

        private readonly Random _rng;
        private readonly string[] _units;
        private readonly string _player;

        /// <summary>Units this generator has put on a scene, so they can be heard clearing it later.</summary>
        private readonly Dictionary<string, string> _onScene = new Dictionary<string, string>();

        public RadioTraffic(string playerUnit, int seed)
        {
            _player = playerUnit ?? "";
            _rng = new Random(seed);

            var units = new List<string>();
            var prefixes = new[] { "1A", "1L", "2A", "3A", "4K", "6A", "7L", "8A" };
            while (units.Count < 8)
            {
                var unit = prefixes[_rng.Next(prefixes.Length)] + _rng.Next(10, 99);
                if (unit != _player && !units.Contains(unit)) units.Add(unit);
            }
            _units = units.ToArray();
        }

        public string[] Units { get { return _units; } }

        /// <summary>One transmission from somewhere else in the city.</summary>
        public string Next()
        {
            // A unit that went on scene clears it about a third of the time, so the radio has continuity.
            if (_onScene.Count > 0 && _rng.Next(100) < 35)
            {
                var keys = new List<string>(_onScene.Keys);
                var unit = keys[_rng.Next(keys.Count)];
                var where = _onScene[unit];
                _onScene.Remove(unit);
                return Pick(
                    unit + ", 10-98 from " + where + ", back in service.",
                    unit + " clear of " + where + ", code 4, show me available.",
                    "Copy " + unit + ", 10-8 at " + Clock() + ".");
            }

            var who = _units[_rng.Next(_units.Length)];
            var other = _units[_rng.Next(_units.Length)];
            var place = Districts[_rng.Next(Districts.Length)];
            var call = Calls[_rng.Next(Calls.Length)];

            switch (_rng.Next(7))
            {
                case 0:
                case 1:
                    _onScene[who] = place;
                    return who + ", 10-97 at " + place + " on " + call + ".";
                case 2:
                    return "Dispatch, " + who + ", can I get a plate run, " + Plate() + "? ... Copy, comes back clean.";
                case 3:
                    return who + ", show me 10-6 on a traffic stop, " + place + ".";
                case 4:
                    return other == who
                        ? who + ", 10-8, available for calls."
                        : "Any unit to assist " + who + " at " + place + ", code 2. ... " + other + " responding.";
                case 5:
                    return who + " is 10-7 for a meal break at " + place + ".";
                default:
                    return "All units, be advised, " + call + " reported near " + place + ". " + who + " has it.";
            }
        }

        private string Pick(params string[] options) { return options[_rng.Next(options.Length)]; }

        private string Plate()
        {
            const string letters = "ABCDEFGHJKLMNPRSTUVWXYZ";
            return "" + _rng.Next(1, 9) + letters[_rng.Next(letters.Length)] + letters[_rng.Next(letters.Length)] +
                   letters[_rng.Next(letters.Length)] + _rng.Next(100, 999);
        }

        private static string Clock() { return DateTime.Now.ToString("HH:mm"); }
    }
}
