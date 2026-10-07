using System;
using System.Collections.Generic;
using LSPD_First_Response.Mod.API;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// Which agency the player is on duty as, and whether a callout belongs to it.
    ///
    /// LSPDFR gives a callout exactly one way to ask - Functions.GetCurrentAgencyScriptName(), the
    /// agency's ScriptName from agency.xml - and nothing that filters callouts for you. One registry
    /// holds every callout whatever you are wearing, so a pack that wants "go on duty as the Sheriff
    /// and get county work, or as EMS and get patients" has to decide for itself.
    ///
    /// A recipe says which duty it belongs to in its &lt;For&gt; element, and can name more than one,
    /// separated by commas:
    ///
    ///     police              any police agency - the default
    ///     lspd, sheriff, sahp, nysp, ranger, prison, fib, iaa, noose, doa, swat
    ///                         one agency family, by the name a player would use for it
    ///     ems, fire           the medical and fire branches (a fire crew is also offered the
    ///                         medical work, because LSFD Fire runs medical calls and the pack's own
    ///                         fire callout - a vehicle fire - is written as a medical one)
    ///     any                 anybody, including agencies nobody here has heard of
    ///
    /// The names are matched loosely, because agencies are named in agency.xml by whoever wrote the
    /// file: "sheriff" finds lssd, "ranger" finds sapr, and an agency that matches nothing at all is
    /// treated as police. A patrol with no calls at all is a worse failure than a misfiled callout.
    /// </summary>
    internal static class Agency
    {
        public const string Police = "police";
        public const string Medical = "ems";
        public const string Fire = "fire";
        public const string Any = "any";

        private static readonly string[] MedicalWords = { "lsfd", "ems", "medic", "paramedic", "ambulance" };
        private static readonly string[] FireWords = { "lsfd_fire", "fire" };

        /// <summary>What a player would call an agency, and what to look for in its name.</summary>
        private static readonly Dictionary<string, string[]> Families =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "lspd",     new[] { "lspd" } },
                { "sheriff",  new[] { "lssd", "sheriff", "county" } },
                { "lssd",     new[] { "lssd", "sheriff", "county" } },
                { "sahp",     new[] { "sahp", "highway", "patrol" } },
                { "highway",  new[] { "sahp", "highway", "patrol" } },
                { "nysp",     new[] { "nysp", "yankton" } },
                { "ranger",   new[] { "sapr", "ranger", "park" } },
                { "sapr",     new[] { "sapr", "ranger", "park" } },
                { "prison",   new[] { "saspa", "prison", "penitentiary", "bolingbroke" } },
                { "saspa",    new[] { "saspa", "prison", "penitentiary", "bolingbroke" } },
                { "fib",      new[] { "fib" } },
                { "iaa",      new[] { "iaa" } },
                { "noose",    new[] { "noose" } },
                { "doa",      new[] { "doa", "drug observation" } },
                { "swat",     new[] { "swat" } },
            };

        /// <summary>What the player is working as, or null when LSPDFR will not say.</summary>
        public static string Current()
        {
            try { return Functions.GetCurrentAgencyScriptName(); }
            catch (Exception ex) { Log.Error("asking which agency the player is on duty as", ex); return null; }
        }

        public static bool IsMedical(string agency) { return Contains(agency, MedicalWords); }
        public static bool IsFire(string agency) { return Contains(agency, FireWords); }

        /// <summary>
        /// Whether a callout that says `wanted` should be offered on this duty. `wanted` may be a list.
        /// </summary>
        public static bool AllowsFor(string wanted)
        {
            return AllowsFor(wanted, Current());
        }

        public static bool AllowsFor(string wanted, string agency)
        {
            var kinds = Split(wanted);
            if (kinds.Length == 0) return true;

            // The agency's own category, decided once: fire and medical first, because lsfd_fire
            // contains "lsfd" and is the fire branch, and everything else is police - including an
            // agency whose name this file has never seen.
            var category = IsFire(agency) ? Fire : IsMedical(agency) ? Medical : Police;

            foreach (var kind in kinds)
            {
                if (kind == Any) return true;
                if (kind == category) return true;

                // The fire branch is the medical branch as well.
                //
                // LSPDFR splits LSFD in two - lsfd is the ambulance, lsfd_fire is the fire truck - and
                // this file treated them as separate worlds, so a fire crew was offered "fire" work and
                // nothing else. That is not how the department works, and it is not how this pack is
                // written: fire crews run medical calls, and the pack's own fire callout, Vehicle Fire,
                // is a medical one - so a fire patrol was offered neither the medical callouts nor its
                // own. Worse than it sounds, because it is a fire patrol with the *fewest* callouts in
                // the pack: 0 of the 18 built-ins and a small slice of the library.
                //
                // One way only. A fire crew gets the medical work; an ambulance is still not sent to a
                // fire.
                if (category == Fire && kind == Medical) return true;

                // An agency family by the name a player would use for it.
                string[] words;
                if (Families.TryGetValue(kind, out words) && Contains(agency, words)) return true;
            }

            return false;
        }

        private static string[] Split(string wanted)
        {
            if (string.IsNullOrEmpty(wanted)) return new string[0];

            var parts = wanted.Trim().ToLowerInvariant()
                .Split(new[] { ',', ' ', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);

            // "medical" is how a recipe author would say it; the pack calls that category ems.
            for (var i = 0; i < parts.Length; i++)
                if (parts[i] == "medical") parts[i] = Medical;

            return parts;
        }

        private static bool Contains(string agency, string[] words)
        {
            if (string.IsNullOrEmpty(agency)) return false;

            var name = agency.Trim().ToLowerInvariant();
            foreach (var word in words)
                if (name.IndexOf(word, StringComparison.Ordinal) >= 0) return true;

            return false;
        }
    }
}
