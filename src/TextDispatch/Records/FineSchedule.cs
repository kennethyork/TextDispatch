using System;
using System.Collections.Generic;

namespace TextDispatch.Records
{
    /// <summary>
    /// What a citation costs, by offence. A flat fine for everything made running a red light cost the
    /// same as driving without insurance; this is the schedule a citation is written from instead.
    ///
    /// Matched on words, the way an officer would type it - "speeding", "ran a red light", "no
    /// insurance" - and the first entry whose words all appear wins, so the more particular entries
    /// come first. Anything it does not know is the default fine, and says so.
    /// </summary>
    public static class FineSchedule
    {
        public const double Default = 250.0;

        public sealed class Entry
        {
            public string Offence;
            public double Fine;
            public string[] Words;   // every one of these must appear (any of the |-separated forms)
        }

        private static Entry E(string offence, double fine, params string[] words)
        {
            return new Entry { Offence = offence, Fine = fine, Words = words };
        }

        public static readonly List<Entry> All = new List<Entry>
        {
            E("speeding, 25+ over the limit", 500, "speed|speeding", "25|30|40|50|excessive|reckless"),
            E("speeding", 200, "speed|speeding|over the limit"),
            E("running a red light", 300, "red light|ran the light|ran a light|run the light|ran the red|ran a red"),
            E("failure to stop at a stop sign", 200, "stop sign"),
            E("failure to yield", 200, "yield"),
            E("illegal U-turn", 150, "u-turn|uturn|u turn"),
            E("reckless driving", 750, "reckless"),
            E("careless driving", 350, "careless"),
            E("driving on the wrong side of the road", 500, "wrong way|wrong side"),
            E("driving without insurance", 600, "insurance|uninsured"),
            E("driving without a licence", 500, "licence|license|unlicensed"),
            E("expired registration", 150, "registration|expired|tags|reg"),
            E("no seatbelt", 100, "seatbelt|seat belt|belt"),
            E("using a phone while driving", 250, "phone|texting|mobile"),
            E("broken tail light", 75, "tail light|taillight|headlight|light out"),
            E("tinted windows", 100, "tint|tinted"),
            E("illegal parking", 80, "parking|parked"),
            E("blocking traffic", 150, "blocking|obstruct"),
            E("excessive noise", 100, "noise|loud|exhaust|horn"),
            E("open container", 300, "open container|drinking|alcohol"),
            E("jaywalking", 50, "jaywalk|jaywalking|crossing"),
            E("littering", 100, "litter|littering"),
            E("public intoxication", 250, "intoxicat|drunk"),
            E("disorderly conduct", 300, "disorderly|disturbance|fighting"),
            E("trespassing", 350, "trespass"),
            E("loitering", 100, "loiter"),
            E("vandalism", 400, "vandal|graffiti|damage"),
            E("possession of cannabis", 300, "cannabis|weed|marijuana"),
        };

        /// <summary>The entry for what was typed, or null if nothing in the schedule fits.</summary>
        public static Entry Find(string offence)
        {
            var text = " " + (offence ?? "").Trim().ToLowerInvariant() + " ";
            if (text.Trim().Length == 0) return null;

            foreach (var entry in All)
            {
                var all = true;
                foreach (var word in entry.Words)
                {
                    var any = false;
                    foreach (var form in word.Split('|'))
                        if (text.IndexOf(form, StringComparison.Ordinal) >= 0) { any = true; break; }
                    if (!any) { all = false; break; }
                }
                if (all) return entry;
            }

            return null;
        }

        /// <summary>
        /// Take a trailing "$400" off an offence, if the officer named the amount themselves.
        /// "speeding $400" leaves "speeding" behind and returns 400.
        /// </summary>
        public static double? Amount(ref string offence)
        {
            var text = (offence ?? "").Trim();
            var at = text.LastIndexOf('$');
            if (at < 0) return null;

            double amount;
            if (!double.TryParse(text.Substring(at + 1).Trim(), System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out amount) || amount <= 0 || amount > 100000)
                return null;

            offence = text.Substring(0, at).Trim();
            return amount;
        }
    }
}
