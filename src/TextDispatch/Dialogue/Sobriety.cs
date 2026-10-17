using System;

namespace TextDispatch.Dialogue
{
    /// <summary>
    /// How much somebody has had to drink. Decided once per person in the world, from the handle, so the
    /// smell /examine notices, what they say when asked, the field sobriety tests and the breath test all
    /// agree - and asking again gives the same answer.
    ///
    /// Most people have had nothing. About one in nine has: a beer or two, under the limit; over it; or
    /// well over it. A DUI suspect from a callout or a driver LSPDFR flags is set drunk by whoever knows.
    /// </summary>
    internal static class Sobriety
    {
        public const double Limit = 0.08;

        private static readonly System.Collections.Generic.Dictionary<int, double> Set =
            new System.Collections.Generic.Dictionary<int, double>();

        /// <summary>Blood alcohol content, 0.00 to about 0.25.</summary>
        public static double Bac(int handle)
        {
            double set;
            if (Set.TryGetValue(handle, out set)) return set;

            var rng = new Random(unchecked(handle * 6007 + 19));
            var roll = rng.Next(100);
            if (roll < 89) return 0.0;
            if (roll < 93) return 0.02 + rng.Next(5) / 100.0;          // a beer or two
            if (roll < 98) return 0.08 + rng.Next(7) / 100.0;          // over the limit
            return 0.15 + rng.Next(10) / 100.0;                        // well over it
        }

        public static void Force(int handle, double bac)
        {
            Set[handle] = Math.Max(0, Math.Min(0.4, bac));
        }

        public static bool Drinking(int handle) { return Bac(handle) > 0.0; }

        public static bool Impaired(int handle) { return Bac(handle) >= Limit; }

        /// <summary>
        /// The three standardised field sobriety tests and what an officer sees. Clues follow the blood
        /// alcohol, with a little noise from nerves - a sober and nervous person can wobble once.
        /// </summary>
        public static string Test(int handle, string which, out int clues, out int of)
        {
            var bac = Bac(handle);
            var rng = new Random(unchecked(handle * 31 + which.GetHashCode()));
            var nerves = rng.Next(4) == 0 ? 1 : 0;

            switch (which)
            {
                case "hgn":
                    of = 6;
                    clues = bac >= 0.08 ? 4 + rng.Next(3) : bac > 0.04 ? 2 + rng.Next(2) : 0;
                    return "Horizontal gaze nystagmus: " + clues + " of 6 clues" +
                           (clues >= 4 ? " - lack of smooth pursuit and distinct nystagmus in both eyes, onset before 45 degrees."
                            : clues > 0 ? " - some lack of smooth pursuit." : " - eyes track smoothly.");
                case "walk":
                    of = 8;
                    clues = Math.Min(8, (bac >= 0.08 ? 3 + rng.Next(4) : bac > 0.04 ? 1 + rng.Next(2) : 0) + nerves);
                    return "Walk and turn: " + clues + " of 8 clues" +
                           (clues >= 2 ? " - " + (clues >= 4 ? "steps off the line, misses heel-to-toe, uses arms for balance, an improper turn."
                                                              : "misses heel-to-toe, raises the arms for balance.")
                            : clues == 1 ? " - one step off the line." : " - nine steps out and back, heel to toe, a correct turn.");
                default:
                    of = 4;
                    clues = Math.Min(4, (bac >= 0.08 ? 2 + rng.Next(3) : bac > 0.04 ? rng.Next(2) : 0) + (nerves > 0 && bac == 0 ? 0 : 0));
                    return "One-leg stand: " + clues + " of 4 clues" +
                           (clues >= 2 ? " - sways, puts the foot down" + (clues >= 3 ? ", hops, raises the arms." : ".")
                            : clues == 1 ? " - sways slightly." : " - holds it for thirty seconds without trouble.");
            }
        }

        /// <summary>A preliminary breath test reading: the BAC, read to two places with a little meter error.</summary>
        public static double Breath(int handle)
        {
            var bac = Bac(handle);
            if (bac <= 0) return 0.0;
            var rng = new Random(unchecked(handle * 13 + Environment.TickCount / 60000));
            return Math.Max(0.0, Math.Round(bac + (rng.Next(5) - 2) / 100.0, 2));
        }
    }
}
