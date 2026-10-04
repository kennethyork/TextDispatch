using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using LSPD_First_Response.Mod.Callouts;

namespace TextCallouts.Custom
{
    internal enum Resolution
    {
        /// <summary>Every suspect arrested or dead.</summary>
        ArrestOrDeath,

        /// <summary>The first suspect arrested is enough.</summary>
        AnyArrest,

        /// <summary>Nothing to catch: the callout closes when it is told to (a collision, a welfare check).</summary>
        Manual
    }

    internal sealed class ActorRecipe
    {
        public string Model = "a_m_y_business_01";
        public string Role = "Suspect";          // Suspect | Bystander
        public bool Armed;
        public string Weapon = "WEAPON_PISTOL";
        public int Ammo = 120;
        public int Armor;
        public int Accuracy = 45;
        public bool Hostile;
        public bool Cower;
        public string Vehicle;                   // a model name, or null: spawn one and put them in it
        public int Count = 1;

        public bool IsSuspect { get { return !"Bystander".Equals(Role, StringComparison.OrdinalIgnoreCase); } }
    }

    /// <summary>
    /// A callout written in a file instead of in C#.
    ///
    /// Everything the pack's own eight callouts do, this can do: choose a place, put people and
    /// vehicles there, decide what they do when the player arrives, choose what dispatch says, and
    /// decide what ends it. The point is that adding a callout should not need a compiler.
    /// </summary>
    internal sealed class CalloutRecipe
    {
        /// <summary>The file it came from, for the log. Named Source rather than File so that the
        /// field does not shadow System.IO.File inside this class.</summary>
        public string Source;
        public string Id;
        public string Name = "Custom Callout";
        public string Message = "A callout is waiting for you.";
        public string Advisory;
        public CalloutProbability Probability = CalloutProbability.Medium;

        public float DistanceMin = 150f;
        public float DistanceMax = 320f;
        public float BlipRadius = 40f;
        public int TimeoutMinutes = 20;

        public Resolution Resolution = Resolution.ArrestOrDeath;

        public readonly List<ActorRecipe> Actors = new List<ActorRecipe>();

        public float ApproachDistance = 25f;
        public bool ApproachFlee;
        public bool ApproachFleeInVehicle;
        public bool ApproachHostile;
        public bool ApproachHandsUp;
        public bool ApproachCower;

        public bool RequestAmbulance;
        public bool RequestBackup;

        public string Briefing;
        public string ApproachLine;
        public string ResolvedLine;
        public string SignOff;

        /// <summary>What the log calls this, and what /callout accepts.</summary>
        public string Display { get { return Name + "  (" + Id + ")"; } }

        // ------------------------------------------------------------------ parsing

        /// <summary>Read one recipe. Throws on anything it cannot make sense of - see Load for why.</summary>
        public static CalloutRecipe Parse(string path)
        {
            var recipe = new CalloutRecipe();
            recipe.Source = Path.GetFileName(path);
            recipe.Id = SafeId(Path.GetFileNameWithoutExtension(path));

            XDocument document;
            using (var stream = File.OpenRead(path)) document = XDocument.Load(stream);

            var root = document.Root;
            if (root == null) throw new InvalidDataException("the file is empty");

            foreach (var element in root.Elements())
            {
                switch (element.Name.LocalName.ToLowerInvariant())
                {
                    case "id": recipe.Id = SafeId(Text(element)); break;
                    case "name": recipe.Name = Text(element); break;
                    case "message": recipe.Message = Text(element); break;
                    case "advisory": recipe.Advisory = Text(element); break;
                    case "probability": recipe.Probability = ProbabilityOf(element); break;
                    case "timeoutminutes": recipe.TimeoutMinutes = Int(element, recipe.TimeoutMinutes); break;
                    case "resolution": recipe.Resolution = ResolutionOf(Text(element)); break;

                    case "distance":
                        recipe.DistanceMin = Attribute(element, "Min", recipe.DistanceMin);
                        recipe.DistanceMax = Attribute(element, "Max", recipe.DistanceMax);
                        recipe.BlipRadius = Attribute(element, "Radius", recipe.BlipRadius);
                        break;

                    case "actors":
                        foreach (var child in element.Elements())
                        {
                            if (child.Name.LocalName.ToLowerInvariant() != "ped") continue;
                            recipe.Actors.Add(Actor(child));
                        }
                        break;

                    case "onapproach":
                        recipe.ApproachDistance = Attribute(element, "At", recipe.ApproachDistance);
                        recipe.ApproachFlee = Flag(element, "Flee", false);
                        recipe.ApproachFleeInVehicle = Flag(element, "FleeInVehicle", false);
                        recipe.ApproachHostile = Flag(element, "Hostile", false);
                        recipe.ApproachHandsUp = Flag(element, "HandsUp", false);
                        recipe.ApproachCower = Flag(element, "Cower", false);
                        break;

                    case "support":
                        recipe.RequestAmbulance = Flag(element, "Ambulance", false);
                        recipe.RequestBackup = Flag(element, "Backup", false);
                        break;

                    case "lines":
                        foreach (var child in element.Elements())
                        {
                            switch (child.Name.LocalName.ToLowerInvariant())
                            {
                                case "briefing": recipe.Briefing = Text(child); break;
                                case "approach": recipe.ApproachLine = Text(child); break;
                                case "resolved": recipe.ResolvedLine = Text(child); break;
                                case "signoff": recipe.SignOff = Text(child); break;
                                default: throw new InvalidDataException("<Lines> has no <" + child.Name.LocalName + ">");
                            }
                        }
                        break;

                    case "notes":
                    case "comment":
                        break;

                    default:
                        throw new InvalidDataException("unknown element <" + element.Name.LocalName + ">");
                }
            }

            if (recipe.Actors.Count == 0)
            {
                // A callout with nobody in it is a mistake rather than an idea, and saying so beats
                // spawning an empty street the player will assume is a bug.
                throw new InvalidDataException("no <Actors> - a callout needs at least one <Ped>");
            }

            if (recipe.DistanceMax < recipe.DistanceMin) recipe.DistanceMax = recipe.DistanceMin;
            if (recipe.TimeoutMinutes < 1) recipe.TimeoutMinutes = 1;

            return recipe;
        }

        private static ActorRecipe Actor(XElement element)
        {
            var actor = new ActorRecipe();
            actor.Model = AttributeText(element, "Model", actor.Model);
            actor.Role = AttributeText(element, "Role", actor.Role);
            actor.Armed = Flag(element, "Armed", actor.Armed);
            actor.Weapon = AttributeText(element, "Weapon", actor.Weapon);
            actor.Ammo = (int)Attribute(element, "Ammo", actor.Ammo);
            actor.Armor = (int)Attribute(element, "Armor", actor.Armor);
            actor.Accuracy = (int)Attribute(element, "Accuracy", actor.Accuracy);
            actor.Hostile = Flag(element, "Hostile", actor.Hostile);
            actor.Cower = Flag(element, "Cower", actor.Cower);
            actor.Vehicle = AttributeText(element, "Vehicle", actor.Vehicle);
            actor.Count = (int)Attribute(element, "Count", actor.Count);
            if (actor.Count < 1) actor.Count = 1;
            if (actor.Count > 8) actor.Count = 8;
            return actor;
        }

        private static string Text(XElement element)
        {
            return (element.Value ?? "").Trim();
        }

        private static float Attribute(XElement element, string name, float fallback)
        {
            var attribute = element.Attribute(name);
            if (attribute == null) return fallback;

            float value;
            if (float.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;

            throw new InvalidDataException("'" + attribute.Value + "' is not a number for " + name);
        }

        private static string AttributeText(XElement element, string name, string fallback)
        {
            var attribute = element.Attribute(name);
            return attribute == null ? fallback : attribute.Value.Trim();
        }

        private static bool Flag(XElement element, string name, bool fallback)
        {
            var attribute = element.Attribute(name);
            if (attribute == null) return fallback;

            var value = attribute.Value.Trim().ToLowerInvariant();
            if (value == "true" || value == "yes" || value == "1") return true;
            if (value == "false" || value == "no" || value == "0") return false;

            throw new InvalidDataException("'" + attribute.Value + "' is not true or false for " + name);
        }

        private static int Int(XElement element, int fallback)
        {
            int value;
            if (int.TryParse(element.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return value;
            return fallback;
        }

        private static CalloutProbability ProbabilityOf(XElement element)
        {
            try { return (CalloutProbability)Enum.Parse(typeof(CalloutProbability), Text(element), true); }
            catch
            {
                throw new InvalidDataException("'" + Text(element) + "' is not a probability - " +
                                               "use VeryLow, Low, Medium, High or VeryHigh");
            }
        }

        private static Resolution ResolutionOf(string text)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "anyarrest": return Resolution.AnyArrest;
                case "manual": return Resolution.Manual;
                case "arrestordeath": return Resolution.ArrestOrDeath;
                default:
                    throw new InvalidDataException("'" + text + "' is not a resolution - " +
                                                   "use ArrestOrDeath, AnyArrest or Manual");
            }
        }

        /// <summary>An id that can be typed and used as a type name.</summary>
        public static string SafeId(string text)
        {
            var id = (text ?? "callout").Trim();
            var clean = new System.Text.StringBuilder();
            foreach (var character in id)
            {
                if (char.IsLetterOrDigit(character)) clean.Append(character);
                else if (clean.Length > 0 && clean[clean.Length - 1] != '_') clean.Append('_');
            }

            var result = clean.ToString().Trim('_');
            if (result.Length == 0) result = "callout";
            if (char.IsDigit(result[0])) result = "c" + result;
            return result;
        }
    }
}
