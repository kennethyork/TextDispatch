using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using LSPD_First_Response.Mod.Callouts;

namespace TextCallouts.Custom
{
    /// <summary>
    /// Finds the callouts the player has written, turns each one into a real callout type, and hands
    /// them to the pack to register alongside its own.
    ///
    /// LSPDFR registers callouts as *types* - Functions.RegisterCallout takes a Type and instantiates
    /// it when dispatch wants to offer that callout - so a callout that exists only as a text file
    /// needs a type built for it at run time. That is what the small dynamic assembly here is for:
    /// one subclass of RecipeCallout per file, each carrying its own name and probability in a
    /// [CalloutInfo] attribute, each bound to the recipe it came from.
    ///
    /// If any of that fails the file is named in the log and skipped. The built-in eight never depend
    /// on it, so a mistake in a recipe cannot cost the player the pack.
    /// </summary>
    internal static class CustomCallouts
    {
        private static readonly List<CalloutRecipe> Recipes = new List<CalloutRecipe>();
        private static readonly List<Type> Types = new List<Type>();
        private static ModuleBuilder _module;
        private static bool _loaded;
        private static int _problems;

        internal static string Folder
        {
            get { return Path.Combine(Log.PluginFolder(), "TextCallouts", "Custom"); }
        }

        /// <summary>The callout types built from the player's files. Loads them on first call.</summary>
        internal static Type[] CalloutTypes()
        {
            Load();
            return Types.ToArray();
        }

        internal static int Count { get { Load(); return Types.Count; } }

        internal static int Problems { get { Load(); return _problems; } }

        internal static List<string> Names()
        {
            Load();
            var names = new List<string>();
            foreach (var recipe in Recipes) names.Add(recipe.Display);
            return names;
        }

        // ------------------------------------------------------------------ loading

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                EnsureFolder();

                if (!Directory.Exists(Folder))
                {
                    Log.Line("custom callouts: no folder at " + Folder);
                    return;
                }

                var files = Directory.GetFiles(Folder, "*.xml");
                foreach (var file in files)
                {
                    try
                    {
                        var recipe = CalloutRecipe.Parse(file);
                        var type = Emit(recipe);
                        if (type == null) { _problems++; continue; }

                        RecipeCallout.Bind(type.Name, recipe);
                        Recipes.Add(recipe);
                        Types.Add(type);
                        Log.Line("custom callout: " + recipe.Display + "  <-  " + Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _problems++;
                        // A file somebody wrote by hand is the most likely thing here to be wrong, and
                        // skipping it in silence would look exactly like the file never being read.
                        Log.Line("custom callout NOT loaded: " + Path.GetFileName(file) + ": " + ex.Message);
                    }
                }

                Log.Line("custom callouts: " + Types.Count + " loaded, " + _problems + " skipped, from " + Folder);
                if (Types.Count == 0 && files.Length == 0)
                    Log.Line("custom callouts: put .xml files in that folder to add your own callouts " +
                             "(see README.txt in it)");
            }
            catch (Exception ex) { Log.Error("loading custom callouts", ex); }
        }

        /// <summary>Build the type LSPDFR needs for one recipe.</summary>
        private static Type Emit(CalloutRecipe recipe)
        {
            try
            {
                var name = UniqueName(recipe.Id);
                var builder = Module.DefineType("TextCallouts.CustomCallouts." + name,
                                                TypeAttributes.Public | TypeAttributes.Class,
                                                typeof(RecipeCallout));
                builder.DefineDefaultConstructor(MethodAttributes.Public);

                // The attribute is what LSPDFR and the other plugins read the callout's name and
                // probability from, so the real name goes on the real type.
                var constructor = typeof(CalloutInfoAttribute).GetConstructor(new[] { typeof(string), typeof(CalloutProbability) });
                if (constructor != null)
                {
                    builder.SetCustomAttribute(new CustomAttributeBuilder(
                        constructor, new object[] { recipe.Name, recipe.Probability }));
                }

                return builder.CreateType();
            }
            catch (Exception ex)
            {
                Log.Line("could not build a callout type for " + recipe.Id + " (" + ex.GetType().Name +
                         ": " + ex.Message + ") - that file is skipped, the rest still work");
                return null;
            }
        }

        private static string UniqueName(string id)
        {
            var name = id;
            var suffix = 2;
            while (Types.Exists(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
                name = id + suffix++;

            return name;
        }

        private static ModuleBuilder Module
        {
            get
            {
                if (_module != null) return _module;

                var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    new AssemblyName("TextCallouts.CustomCallouts"), AssemblyBuilderAccess.Run);
                _module = assembly.DefineDynamicModule("TextCallouts.CustomCallouts");
                return _module;
            }
        }

        // ------------------------------------------------------------------ the folder

        /// <summary>
        /// Make the folder, and put the instructions and a worked example in it. A feature whose format
        /// is documented only in a README on a website is a feature nobody uses.
        /// </summary>
        private static void EnsureFolder()
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

                var readme = Path.Combine(Folder, "README.txt");
                if (!File.Exists(readme)) File.WriteAllText(readme, Instructions);

                var example = Path.Combine(Folder, "Example-ArmedRobbery.xml.example");
                if (!File.Exists(example) && Directory.GetFiles(Folder, "*.xml").Length == 0)
                    File.WriteAllText(example, Example);
            }
            catch (Exception ex) { Log.Error("preparing the custom callout folder", ex); }
        }

        private const string Instructions = @"YOUR OWN CALLOUTS
=================

Drop an .xml file in this folder, go on duty, and it is offered like any other callout. No
compiling, no restart - just this folder and a text file.

Start from Example-ArmedRobbery.xml.example: copy it, rename it to end in .xml, and edit it. Files
ending in .example are ignored, so the example does not load until you do that.

Every field, and nothing more than you need:

  <Name>          what dispatch calls it. Shows in /calls and is what /callout takes.
  <Probability>   VeryLow | Low | Medium | High | VeryHigh - how often it is offered.
  <Message>       the line dispatch reads out when it is offered.
  <Advisory>      the second line - the description of who or what is there.
  <TimeoutMinutes> how long it may run before it closes itself. Default 20.
  <Resolution>    ArrestOrDeath  every suspect arrested or dead (the default)
                  AnyArrest      the first arrest is enough
                  Manual         nothing to catch; it closes on the timer
  <Distance Min Max Radius>   how far away it appears (metres), and the area blip on the map.
  <Actors>
     <Ped ... />   one per person. Attributes:
                     Model      any GTA ped model, e.g. a_m_y_business_01
                     Role       Suspect (counts towards the resolution) or Bystander
                     Count      how many of them, 1-8
                     Armed      true/false
                     Weapon     e.g. WEAPON_PISTOL, WEAPON_MICROSMG
                     Ammo       rounds
                     Armor      0-100
                     Accuracy   0-100
                     Hostile    true = fights you from the start
                     Cower      true = takes cover and stays out of it
                     Vehicle    a model name, e.g. sultan - spawns the car and puts them in it
  <OnApproach At=""30"" ...>   what they do when you are that close:
                     Flee           runs on foot
                     FleeInVehicle  runs in the car, driven by LSPDFR's pursuit AI
                     Hostile        turns on you
                     HandsUp        gives up
                     Cower          takes cover
  <Lines>
     <Briefing>   said when you accept
     <Approach>   said when you get close
     <Resolved>   said when it ends
     <SignOff>    said last - defaults to Resolved
  <Support Ambulance=""true"" Backup=""true"" />   who else is sent

A misspelled element or a bad value is not ignored. The file is skipped and the reason is written to
Plugins\LSPDFR\textcallouts.log, naming the file - a callout that never happens is impossible to
debug from inside the game.

In the F4 console, tcstatus says how many custom callouts loaded, and tccallouts lists them.
";

        private const string Example = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!--
  A worked example. Copy this file, rename it so it ends in .xml, and edit it.

  This one is deliberately a callout the pack already has, so you can see the shape of a recipe
  before changing anything.
-->
<Callout>
  <Name>Armed Robbery At The Pier</Name>
  <Probability>Medium</Probability>
  <Message>Armed robbery in progress at the pier.</Message>
  <Advisory>Two males, one of them with a handgun. Staff and customers still inside.</Advisory>
  <TimeoutMinutes>20</TimeoutMinutes>
  <Resolution>ArrestOrDeath</Resolution>

  <Distance Min=""150"" Max=""320"" Radius=""40"" />

  <Actors>
    <Ped Model=""a_m_y_musclbeac_01"" Role=""Suspect"" Armed=""true"" Weapon=""WEAPON_PISTOL""
         Ammo=""120"" Hostile=""true"" Armor=""40"" Accuracy=""45"" />
    <Ped Model=""a_m_y_business_01"" Role=""Suspect"" Hostile=""false"" />
    <Ped Model=""a_f_y_business_01"" Role=""Bystander"" Cower=""true"" />
  </Actors>

  <OnApproach At=""30"" Hostile=""true"" />

  <Lines>
    <Briefing>He is armed and he is still there. Get backup rolling before you close in.</Briefing>
    <Approach>He has seen you. He is not putting it down.</Approach>
    <Resolved>The scene is secure and the suspects are in custody.</Resolved>
    <SignOff>Good work. Show me 10-8 when you are clear.</SignOff>
  </Lines>

  <Support Ambulance=""false"" Backup=""false"" />
</Callout>
";
    }
}
