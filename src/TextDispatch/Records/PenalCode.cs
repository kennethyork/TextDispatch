using System;
using System.Collections.Generic;
using System.Globalization;

namespace TextDispatch.Records
{
    /// <summary>
    /// The San Andreas penal code, as a charge sheet is written from it: a section number, a title, a
    /// class, and what a conviction costs in a fine and in days.
    ///
    /// GTA World's officers book people on numbered charges rather than on whatever they typed, and the
    /// court's answer follows the sheet. This is that list. Sections are numbered by title the way the
    /// server's is - (1) crimes against people, (2) property, (3) public order, (4) justice, (5) weapons,
    /// (6) drugs, (7) vehicles - so "4-02" or "402" or "obstruction" all find the same charge.
    ///
    /// No reference to the game, so the matching can be checked outside it.
    /// </summary>
    public static class PenalCode
    {
        public sealed class Charge
        {
            public string Code;      // "4-02"
            public string Title;     // "Obstruction of a peace officer"
            public string Class;     // Infraction, Misdemeanor, Felony
            public double Fine;
            public int Days;         // jail on conviction; 0 for a fine only
            public string[] Words;   // every one must appear, any of the |-separated forms

            public string Line()
            {
                return "PC " + Code + "  " + Title + "  (" + Class.ToLowerInvariant() + ", $" + Fine.ToString("0", CultureInfo.InvariantCulture) +
                       (Days > 0 ? ", " + Days + " days" : "") + ")";
            }
        }

        private static Charge C(string code, string title, string cls, double fine, int days, params string[] words)
        {
            return new Charge { Code = code, Title = title, Class = cls, Fine = fine, Days = days, Words = words };
        }

        private const string I = "Infraction", M = "Misdemeanor", F = "Felony";

        /// <summary>Every charge, in section order. Find picks the most particular one that fits.</summary>
        public static readonly List<Charge> All = new List<Charge>
        {
            // (1) crimes against people
            C("1-01", "Intimidation", M, 500, 10, "intimidat|threat"),
            C("1-02", "Assault", M, 750, 20, "assault"),
            C("1-03", "Assault with a deadly weapon", F, 3000, 120, "assault|adw", "deadly|weapon|knife|gun|bat"),
            C("1-04", "Battery", M, 1000, 30, "battery|punch|hit|beat"),
            C("1-05", "Aggravated battery", F, 4000, 180, "aggravated", "battery"),
            C("1-06", "Kidnapping", F, 10000, 365, "kidnap|abduct"),
            C("1-07", "Attempted murder", F, 20000, 1095, "attempt", "murder"),
            C("1-08", "Murder", F, 50000, 3650, "murder|homicide"),
            C("1-09", "Manslaughter", F, 15000, 730, "manslaughter"),
            C("1-10", "Domestic violence", M, 1500, 45, "domestic"),
            C("1-11", "Stalking", M, 1000, 30, "stalk|harass"),
            C("1-12", "Robbery", F, 5000, 180, "robbery|robbed|mugging|mugged"),
            C("1-13", "Armed robbery", F, 10000, 365, "armed", "robbery"),
            // (2) property
            C("2-01", "Petty theft", M, 500, 10, "petty|shoplift"),
            C("2-02", "Grand theft", F, 2500, 90, "grand theft|theft|steal|stole"),
            C("2-03", "Grand theft auto", F, 4000, 120, "gta|auto theft|vehicle theft|grand theft auto|stolen vehicle|stolen car|car theft"),
            C("2-04", "Burglary", F, 3000, 120, "burglary|burglar|break in|breaking"),
            C("2-05", "Possession of burglary tools", M, 750, 15, "burglary tools|lock pick|lockpick|slim jim"),
            C("2-06", "Vandalism", M, 400, 0, "vandal|graffiti|criminal damage"),
            C("2-07", "Arson", F, 8000, 365, "arson|set fire|fire to"),
            C("2-08", "Trespassing", M, 350, 0, "trespass"),
            C("2-09", "Receiving stolen property", M, 1000, 20, "stolen property|receiving|handling"),
            C("2-10", "Fraud", F, 5000, 120, "fraud|credit card|identity|forgery|scam"),
            C("2-11", "Possession of proceeds of crime", F, 2500, 60, "possession of proceeds|proceeds"),
            // (3) public order
            C("3-01", "Disorderly conduct", M, 300, 0, "disorderly|disturbance|fighting"),
            C("3-02", "Public intoxication", I, 250, 0, "intoxicat|drunk"),
            C("3-03", "Indecent exposure", M, 500, 10, "indecent|exposure"),
            C("3-04", "Loitering", I, 100, 0, "loiter"),
            C("3-05", "Unlawful assembly", M, 500, 0, "unlawful assembly|riot"),
            C("3-06", "Open container", I, 300, 0, "open container"),
            C("3-07", "Animal cruelty", M, 1500, 30, "animal|cruelty"),
            // (4) justice
            C("4-01", "Failure to identify", M, 500, 0, "failure to identify|refus", "identif|id|name"),
            C("4-02", "Obstruction of a peace officer", M, 1000, 15, "obstruct"),
            C("4-03", "Resisting arrest", M, 1500, 30, "resist"),
            C("4-04", "Evading a peace officer", M, 2000, 30, "evad|fled|fleeing|flee"),
            C("4-05", "Felony evading", F, 5000, 120, "felony", "evad|fled|pursuit"),
            C("4-06", "Assault on a peace officer", F, 6000, 180, "assault|battery", "officer|cop|police"),
            C("4-07", "False report to police", M, 1000, 10, "false report|false 911|filing a false"),
            C("4-08", "Failure to appear / outstanding warrant", M, 1000, 15, "warrant|failure to appear|fta"),
            C("4-09", "Failure to pay a citation", M, 500, 0, "failure to pay"),
            C("4-10", "Impersonating a peace officer", F, 5000, 90, "impersonat"),
            C("4-11", "Bribery", F, 5000, 90, "brib"),
            // (5) weapons
            C("5-01", "Brandishing a weapon", M, 1000, 15, "brandish"),
            C("5-02", "Unlicensed firearm", F, 3000, 90, "firearm|pistol|gun|handgun|unlicensed"),
            C("5-03", "Prohibited weapon", F, 5000, 180, "sawn|sawed|automatic|prohibited|shotgun"),
            C("5-04", "Discharging a firearm in public", F, 4000, 120, "discharg|shots fired|shooting"),
            C("5-05", "Concealed weapon", M, 1500, 30, "concealed|knife|blade"),
            // (6) drugs
            C("6-01", "Possession of cannabis", I, 300, 0, "possession of cannabis|cannabis|weed|marijuana"),
            C("6-02", "Possession of a controlled substance", M, 1500, 30, "possession|cocaine|meth|heroin|pills|drugs|controlled"),
            C("6-03", "Possession with intent to sell", F, 6000, 180, "intent|dealing|supply|sell|distribut"),
            C("6-04", "Drug paraphernalia", I, 250, 0, "paraphernalia|pipe|scale"),
            // (7) vehicles
            C("7-01", "Driving under the influence", M, 2000, 10, "dui|dwi|under the influence|drunk driving|drink driving"),
            C("7-02", "Reckless driving", M, 750, 0, "reckless"),
            C("7-03", "Hit and run", F, 4000, 90, "hit and run|leaving the scene"),
            C("7-04", "Driving without a licence", M, 500, 0, "licence|license|unlicensed"),
            C("7-05", "Driving on a suspended licence", M, 1000, 10, "suspended"),
            C("7-06", "Street racing", M, 1500, 15, "racing|street race"),
            C("7-07", "Driving without insurance", I, 600, 0, "insurance|uninsured"),
            C("7-08", "Vehicular manslaughter", F, 15000, 730, "vehicular"),
        };

        /// <summary>A charge by its section: "4-02", "402", "PC 4-02", "pc402".</summary>
        public static Charge ByCode(string text)
        {
            var t = (text ?? "").Trim().ToLowerInvariant().Replace("pc", "").Replace(" ", "").Replace(".", "-");
            if (t.Length == 0) return null;
            foreach (var charge in All)
            {
                if (charge.Code == t) return charge;
                if (charge.Code.Replace("-", "") == t.Replace("-", "") && t.Replace("-", "").Length >= 3) return charge;
            }
            return null;
        }

        /// <summary>What was typed: a section number, or the words of a charge. Null when nothing fits.</summary>
        public static Charge Find(string text)
        {
            var byCode = ByCode(text);
            if (byCode != null) return byCode;

            var t = " " + (text ?? "").Trim().ToLowerInvariant() + " ";
            if (t.Trim().Length == 0) return null;

            // The exact title first, so "robbery" is robbery and not the first charge that mentions it.
            foreach (var charge in All)
                if (t.Trim() == charge.Title.ToLowerInvariant()) return charge;

            // Of the charges whose words all appear, the most particular: the one that needed the most
            // words, then the one whose words cover most of what was typed - so "armed robbery" is not
            // robbery, and "drunk driving" is not public intoxication.
            Charge best = null;
            var bestScore = 0;
            foreach (var charge in All)
            {
                var score = 0;
                foreach (var word in charge.Words)
                {
                    var longest = 0;
                    foreach (var form in word.Split('|'))
                        if (form.Length > longest && t.IndexOf(form, StringComparison.Ordinal) >= 0) longest = form.Length;
                    if (longest == 0) { score = 0; break; }
                    score += 100 + longest;
                }
                if (score > bestScore) { best = charge; bestScore = score; }
            }
            return best;
        }

        /// <summary>The charges whose title or words mention the text - for /penal and a "did you mean".</summary>
        public static List<Charge> Search(string text)
        {
            var found = new List<Charge>();
            var t = (text ?? "").Trim();
            foreach (var charge in All)
                if (t.Length == 0 || charge.Title.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    charge.Code.StartsWith(t, StringComparison.OrdinalIgnoreCase) ||
                    charge.Class.Equals(t, StringComparison.OrdinalIgnoreCase))
                    found.Add(charge);

            var best = Find(t);
            if (best != null && !found.Contains(best)) found.Insert(0, best);
            return found;
        }

        /// <summary>The sheet's totals, and its most serious class.</summary>
        public static void Totals(IEnumerable<string> codes, out double fine, out int days, out string worst)
        {
            fine = 0;
            days = 0;
            worst = null;
            foreach (var code in codes)
            {
                var charge = ByCode(code);
                if (charge == null) continue;
                fine += charge.Fine;
                days += charge.Days;
                if (worst == null || Rank(charge.Class) > Rank(worst)) worst = charge.Class;
            }
        }

        private static int Rank(string cls)
        {
            return cls == F ? 3 : cls == M ? 2 : 1;
        }

        /// <summary>"Robbery (PC 1-12) and resisting arrest (PC 4-03)" - how the sheet is said on the radio.</summary>
        public static string Describe(IList<string> codes)
        {
            var parts = new List<string>();
            foreach (var code in codes)
            {
                var charge = ByCode(code);
                parts.Add(charge == null ? code : charge.Title.ToLowerInvariant() + " (PC " + charge.Code + ")");
            }
            if (parts.Count == 0) return "";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray()) + " and " + parts[parts.Count - 1];
        }
    }
}
