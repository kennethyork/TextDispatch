using System;
using System.Collections.Generic;
using TextDispatch.Records;

namespace TextDispatch.Dialogue
{
    /// <summary>
    /// What somebody has in their pockets, decided once per person: a pat-down finds it, and when asked
    /// whether they mind being searched they already know whether there is something to find - so the
    /// answer and the search agree.
    /// </summary>
    internal static class Pockets
    {
        public static readonly string[] Ordinary =
        {
            "a wallet", "a phone", "house keys", "car keys", "a lighter", "loose change", "a pack of gum", "earbuds",
            "a receipt", "a bus pass", "a vape pen", "a pack of cigarettes", "a folded letter", "sunglasses"
        };

        public static readonly string[] Contraband =
        {
            "a small bag of white powder", "a folding knife with a four-inch blade", "a bag of pills with no label",
            "a baggie of cannabis", "a set of brass knuckles", "three credit cards in other people's names",
            "a glass pipe with residue", "a loaded revolver in the waistband"
        };

        /// <summary>
        /// The ordinary things in their pockets, and the illegal one or null. LSPDFR saying they carry
        /// contraband settles it; otherwise their record makes it likelier - priors, a warrant.
        /// </summary>
        public static List<string> Contents(int handle, PersonRecord person, bool lspdfrSaysContraband, out string illegal)
        {
            var rng = new Random(unchecked(handle * 2713 + 5));
            var chance = 8 + (person != null && person.Priors.Count > 0 ? 15 : 0) + (person != null && person.Wanted ? 20 : 0);
            var carrying = lspdfrSaysContraband || rng.Next(100) < chance;

            var found = new List<string>();
            var count = 2 + rng.Next(3);
            while (found.Count < count) { var item = Ordinary[rng.Next(Ordinary.Length)]; if (!found.Contains(item)) found.Add(item); }
            illegal = carrying ? Contraband[rng.Next(Contraband.Length)] : null;
            return found;
        }

        public static bool Hiding(int handle, PersonRecord person, bool lspdfrSaysContraband)
        {
            string illegal;
            Contents(handle, person, lspdfrSaysContraband, out illegal);
            return illegal != null;
        }
    }
}
