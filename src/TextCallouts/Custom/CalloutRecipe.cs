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

    /// <summary>
    /// The person who needs treating, for the recipes that are medical rather than a crime.
    ///
    /// A recipe with one of these has no suspects to deal with: it ends when the player has spent long
    /// enough with the patient, which is the same shape the hand-written medical callouts have.
    /// </summary>
    internal sealed class PatientRecipe
    {
        public string Model = "a_m_m_tramp_01";
        public int TreatmentSeconds = 15;
        public float Range = 2.5f;

        /// <summary>What they say when they come round.</summary>
        public string Line;

        public string Working = "Work the patient - the ambulance is rolling.";
    }

    /// <summary>
    /// Something a recipe does after a while, rather than when the player arrives.
    ///
    /// This is what turns a scene into a sequence: they hold the door for twenty seconds and then come
    /// out, the second car arrives a minute in, the fire is set going after they have had a chance to
    /// talk. Only the actions the engine already knows are allowed, so a stage cannot invent behaviour.
    /// </summary>
    internal sealed class StageRecipe
    {
        public int At = 30;                 // seconds after the player arrives
        public string Do = "line";          // line | flee | fleeinvehicle | hostile | handsup | cower | backup | ambulance | fire | end
        public string Text;                 // for Do="line"
    }

    internal sealed class ActorRecipe
    {
        public string Model = "a_m_y_business_01";

        /// <summary>
        /// The people this actor may be, drawn from at random each time the callout runs - so the man in
        /// an armed robbery is rarely the same man twice. Empty means Model, and only Model.
        ///
        /// A pool is a theme rather than a costume list, and it is never a risk to the scene: the spawn
        /// checks each candidate against the game and falls back if none of them exist, so a pool with a
        /// typo in it cannot leave a callout with nobody in it. See RecipeCallout.PickModel.
        /// </summary>
        public string[] Models = new string[0];
        public string Role = "Suspect";          // Suspect | Bystander
        public bool Armed;
        public string Weapon = "WEAPON_PISTOL";
        public int Ammo = 120;
        public int Armor;
        public int Accuracy = 45;
        public bool Hostile;
        public bool Cower;
        public string Vehicle;                   // a model name, or null: spawn one and put them in it

        /// <summary>The vehicles this actor may arrive in, drawn from at random like the people are.
        /// Empty means Vehicle. A stolen bus is a bus; a car that fails to stop is any car.</summary>
        public string[] Vehicles = new string[0];

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

        /// <summary>
        /// Which duty this belongs to: police, medical, fire, or any.
        ///
        /// Defaults to police, which is what every recipe written before this existed is, and what an
        /// unknown value falls back to - see Callouts.Agency for the rule.
        /// </summary>
        public string For = "police";

        /// <summary>Set for the medical recipes, and then there are no suspects.</summary>
        public PatientRecipe Patient;

        /// <summary>
        /// What the scene's people say when they are spoken to, in order.
        ///
        /// Handed to Scripts.CalloutScript when the scene is built, where TextDispatch finds them - so a
        /// suspect in a recipe answers in character instead of being mute or being improvised at by a
        /// language model.
        /// </summary>
        public readonly List<string> Script = new List<string>();

        /// <summary>What the scene does after the player has been there a while.</summary>
        public readonly List<StageRecipe> Stages = new List<StageRecipe>();

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

        /// <summary>Light a fire at the scene when the player arrives - the fire recipes.</summary>
        public bool ApproachFire;

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
                    case "for": recipe.For = Text(element); break;
                    case "patient": recipe.Patient = PatientOf(element); break;

                    case "script":
                        foreach (var line in element.Elements())
                        {
                            if (line.Name.LocalName.ToLowerInvariant() != "line") continue;
                            var spoken = Text(line);
                            if (!string.IsNullOrEmpty(spoken)) recipe.Script.Add(spoken);
                        }
                        break;

                    case "stages":
                        foreach (var stage in element.Elements())
                        {
                            if (stage.Name.LocalName.ToLowerInvariant() != "stage") continue;

                            var recipeStage = new StageRecipe();
                            recipeStage.At = NumberAttribute(stage, "At", recipeStage.At);
                            recipeStage.Do = (AttributeText(stage, "Do", recipeStage.Do) ?? recipeStage.Do).Trim().ToLowerInvariant();
                            recipeStage.Text = Text(stage);
                            recipe.Stages.Add(recipeStage);
                        }
                        break;
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
                        recipe.ApproachFire = Flag(element, "Fire", false);
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

            // Models="a,b,c" - the pool the actor is picked from. Model is what it falls back to, and
            // is added to the end of the pool so a single-model recipe and a pooled one take the same
            // path through the spawn.
            actor.Models = Pool(element, "Models", actor.Models);
            actor.Vehicles = Pool(element, "Vehicles", actor.Vehicles);
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

        /// <summary>An attribute that should be a whole number. A recipe that gets it wrong keeps the
        /// default rather than failing to load: the sequence is worth more than the exact timing.</summary>
        private static int NumberAttribute(XElement element, string name, int fallback)
        {
            var text = AttributeText(element, name, "");
            int parsed;
            return int.TryParse(text, out parsed) ? parsed : fallback;
        }

        private static PatientRecipe PatientOf(XElement element)
        {
            var patient = new PatientRecipe();
            patient.Model = AttributeText(element, "Model", patient.Model);
            var seconds = AttributeText(element, "TreatmentSeconds", "");
            int parsed;
            if (int.TryParse(seconds, out parsed) && parsed > 0) patient.TreatmentSeconds = parsed;
            patient.Range = Attribute(element, "Range", patient.Range);
            patient.Line = Text(element);
            patient.Working = AttributeText(element, "Working", patient.Working);
            return patient;
        }

        private static float Attribute(XElement element, string name, float fallback)
        {
            var attribute = element.Attribute(name);
            if (attribute == null) return fallback;

            float value;
            if (float.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;

            throw new InvalidDataException("'" + attribute.Value + "' is not a number for " + name);
        }

        /// <summary>One of the "a,b,c" attributes, in the order written and without repeats.</summary>
        private static string[] Pool(XElement element, string name, string[] fallback)
        {
            var text = AttributeText(element, name, null);
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            var names = new List<string>();
            foreach (var part in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;
                if (names.Exists(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
                names.Add(trimmed);
            }

            return names.Count > 0 ? names.ToArray() : fallback;
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
