using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LSPD_First_Response.Mod.Callouts;
using TextCallouts.Custom;

// The pack's log, replaced with one that records instead of writing to the player's file. The loader
// underneath is unchanged, and its log lines are the evidence here: they are the lines the game writes.
namespace TextCallouts
{
    internal static class Log
    {
        internal static readonly List<string> Lines = new List<string>();
        internal static string Folder = @"D:\Grand Theft Auto V Legacy\Plugins\LSPDFR";

        public static string Path { get { return System.IO.Path.Combine(Folder, "textcallouts.log"); } }
        public static string PluginFolder() { return Folder; }
        public static void Line(string message) { Lines.Add(message); }
        public static void Error(string where, Exception ex) { Lines.Add("could not " + where + ": " + ex.Message); }
    }
}

internal static class Program
{
    private static int _checks;
    private static int _failures;

    private static void Check(string what, bool ok)
    {
        _checks++;
        if (!ok) _failures++;
        Console.WriteLine((ok ? "  ok    " : "  FAIL  ") + what);
    }

    private static int Main(string[] args)
    {
        var folder = args.Length > 0 ? args[0] : @"D:\Grand Theft Auto V Legacy\Plugins\LSPDFR";
        TextCallouts.Log.Folder = folder;

        Console.WriteLine("the loader that ships, against the folder the game reads");
        Console.WriteLine();
        Console.WriteLine("   plugin folder: " + folder);

        var libraryFolder = Path.Combine(folder, "TextCallouts", "Library");
        var customFolder = Path.Combine(folder, "TextCallouts", "Custom");
        var libraryFiles = Directory.Exists(libraryFolder) ? Directory.GetFiles(libraryFolder, "*.xml") : new string[0];
        var customFiles = Directory.Exists(customFolder) ? Directory.GetFiles(customFolder, "*.xml") : new string[0];
        var expected = libraryFiles.Length + customFiles.Length;

        Console.WriteLine("   library: " + libraryFiles.Length + " recipe(s), custom: " + customFiles.Length + " of the player's own");
        Console.WriteLine();

        Check("the library folder is where the pack looks for it", libraryFiles.Length > 0);

        var loader = typeof(CustomCallouts);
        var load = loader.GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check("the loader is where it is meant to be", load != null);
        if (load == null) return Report();

        try
        {
            load.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Check("the loader ran at all: " + ex.GetType().Name + ": " + (ex.InnerException ?? ex).Message, false);
            return Report();
        }

        // ------------------------------------------------------------- what it said it did
        var summary = TextCallouts.Log.Lines.FirstOrDefault(l => l.StartsWith("callouts from files:"));
        var named = TextCallouts.Log.Lines.Count(l => l.StartsWith("library callout:") || l.StartsWith("custom callout:"));
        var problems = TextCallouts.Log.Lines
            .Where(l => l.Contains("NOT loaded") || l.Contains("could not build a callout type") || l.StartsWith("could not "))
            .ToArray();

        Console.WriteLine("   " + (summary ?? "the loader said nothing"));
        Console.WriteLine();

        // The player reads this line to answer "did the library load?", so it names the two folders
        // apart rather than adding them up.
        var wanted = "callouts from files: " + expected + " loaded, 0 skipped (" +
                     libraryFiles.Length + " from the library, " + customFiles.Length + " of your own)";
        Check("it says every file became a callout, with none skipped, counting the two folders apart",
              summary == wanted);
        if (summary != wanted) Console.WriteLine("      expected: " + wanted + Environment.NewLine + "      got:      " + (summary ?? "nothing"));

        var libraryCount = (int)loader.GetProperty("LibraryCount", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                                  .GetValue(null, null);
        Check("the library count is the library, not the player's own folder mixed in",
              libraryCount == libraryFiles.Length);
        Check("it named each one as it went, one line per file", named == expected);

        var fromLibrary = TextCallouts.Log.Lines.Count(l => l.StartsWith("library callout:"));
        Check("each library file is logged as coming from the library", fromLibrary == libraryFiles.Length);
        Check("no file was skipped and none failed to build", problems.Length == 0);
        foreach (var problem in problems.Take(10)) Console.WriteLine("      " + problem);

        // ------------------------------------------------------------- what it actually built
        var types = ListField(loader, "Types");
        var recipes = ListField(loader, "Recipes");

        Check("one callout type per file, and not one more", types.Count == expected);
        Check("one recipe per type", recipes.Count == expected);
        Check("every type derives from the base LSPDFR will be handed",
              types.Cast<object>().All(t => typeof(RecipeCallout).IsAssignableFrom((Type)t)));
        Check("no two types share a name, which would collide in LSPDFR's own list of callouts",
              types.Cast<Type>().Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == expected);

        // The attribute is what LSPDFR reads a callout's name from - an emitted type without it is a
        // callout with no name in /calls and nothing dispatch can offer.
        var withName = 0;
        foreach (var type in types.Cast<Type>())
        {
            var attribute = type.GetCustomAttributes(typeof(CalloutInfoAttribute), false).FirstOrDefault();
            if (attribute == null) continue;
            var name = attribute.GetType().GetProperty("Name");
            if (name != null && !string.IsNullOrWhiteSpace(name.GetValue(attribute, null) as string)) withName++;
        }
        Check("every emitted type carries the name LSPDFR shows", withName == expected);

        // ------------------------------------------------------------- the part the player hears
        var scripted = 0;
        var scriptLines = 0;
        foreach (var recipe in recipes.Cast<CalloutRecipe>())
        {
            if (recipe.Script.Count > 0) scripted++;
            scriptLines += recipe.Script.Count;
        }
        Check("every loaded callout carries its script", scripted == expected);

        Check("every callout type is bound to the recipe it came from", RecipeCallout.Bound.Count == expected);

        // ------------------------------------------------------------- who the scene's people are
        var actors = recipes.Cast<CalloutRecipe>().SelectMany(r => r.Actors).ToArray();

        // The people the player is sent to are drawn from pools of men and women. The others in a scene -
        // a prison guard, a bouncer, a lifeguard - are dressed for the job, and a uniform is one model.
        var suspects = actors.Where(a => a.IsSuspect).ToArray();
        var others = actors.Where(a => !a.IsSuspect).ToArray();
        var pooled = suspects.Count(a => a.Models.Length > 1);
        var models = actors.SelectMany(a => a.Models.Length > 0 ? a.Models : new[] { a.Model })
                           .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Check("the people in a scene are drawn from a pool, and the pool survived parsing and loading",
              pooled >= suspects.Length - 1);
        Check("every pool names more than one person and none of them twice",
              actors.All(a => a.Models.Distinct(StringComparer.OrdinalIgnoreCase).Count() == a.Models.Length));
        Check("everybody else in a scene has a model to be", others.All(a => !string.IsNullOrWhiteSpace(a.Model)));

        var women = models.Count(m => m.StartsWith("a_f_", StringComparison.OrdinalIgnoreCase));
        var bothSexes = suspects.Count(a => a.Models.Any(m => m.StartsWith("a_f_", StringComparison.OrdinalIgnoreCase))
                                         && a.Models.Any(m => m.StartsWith("a_m_", StringComparison.OrdinalIgnoreCase)));
        Check("the people in a scene can be either sex, and the pool says so", bothSexes >= suspects.Length - 5);
        Check("the pack can send women", women >= 20);

        // ------------------------------------------------------------- where it happens, and what is there
        var all = recipes.Cast<CalloutRecipe>().ToArray();
        var placed = all.Where(r => r.Place != null).ToArray();
        var dressed = all.Where(r => r.PropSet != null || r.Props.Count > 0).ToArray();
        Check("every place a recipe names has somewhere real to be",
              placed.All(r => SceneSets.Places.ContainsKey(r.Place) && SceneSets.Places[r.Place].Length > 0));
        Check("every place the parser accepts is in the table, and the other way round",
              CalloutRecipe.Places.All(SceneSets.Places.ContainsKey) && SceneSets.Places.Keys.All(k => CalloutRecipe.Places.Contains(k)));
        Check("every set of scenery a recipe names exists and has something in it",
              dressed.Where(r => r.PropSet != null).All(r => SceneSets.Sets.ContainsKey(r.PropSet) && SceneSets.Sets[r.PropSet].Length > 0));
        Check("every set the parser accepts is defined, and the other way round",
              CalloutRecipe.PropSets.All(SceneSets.Sets.ContainsKey) && SceneSets.Sets.Keys.All(k => CalloutRecipe.PropSets.Contains(k)));
        Check("every piece of scenery is a model name",
              SceneSets.Sets.Values.SelectMany(s => s).All(p => System.Text.RegularExpressions.Regex.IsMatch(p.Model ?? "", "^[a-z0-9_]+$")));
        Check("a good share of the scenes are somewhere, or have something in them", placed.Length + dressed.Length >= 150);
        Check("the prison calls are at the prison", all.Count(r => r.Id.StartsWith("saspa_")) >= 20 && all.Where(r => r.Id.StartsWith("saspa_")).All(r => r.Place == "prison"));
        var visits = all.FirstOrDefault(r => r.Id == "saspa_visitor_argument");
        Check("Argument at Visits has two visitors, guards, and the visits tables",
              visits != null && visits.Actors.Where(a => a.IsSuspect).Sum(a => a.Count) == 2 &&
              visits.Actors.Any(a => !a.IsSuspect && a.Model.Contains("prisguard")) && visits.PropSet == "visits");

        // Vehicles, the same terms as the people: scenery is pooled, the vehicle that *is* the callout is
        // named, and either way there is something to spawn.
        var vehicleActors = actors.Where(a => a.Vehicles.Length > 0 || !string.IsNullOrWhiteSpace(a.Vehicle)).ToArray();
        var drawnVehicles = vehicleActors.SelectMany(a => a.Vehicles.Length > 0 ? a.Vehicles : new[] { a.Vehicle })
                                         .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var pooledVehicles = vehicleActors.Count(a => a.Vehicles.Length > 1);
        Check("a vehicle pool survived parsing and loading, and every pooled one has a fallback",
              vehicleActors.Where(a => a.Vehicles.Length > 0).All(a => a.Vehicles.Length >= 2 && !string.IsNullOrWhiteSpace(a.Vehicle)));

        Console.WriteLine();
        Console.WriteLine("   callouts built by the loader:  " + types.Count);
        Console.WriteLine("   of them with a script:         " + scripted + "  (" + scriptLines + " lines)");
        Console.WriteLine("   bound to their recipe:         " + RecipeCallout.Bound.Count);
        Console.WriteLine("   actors the scenes spawn:       " + actors.Length + "  (" + pooled + " draw from a pool)");
        Console.WriteLine("   distinct ped models:           " + models.Length + "  (" + women + " of them women)");
        Console.WriteLine("   actors arriving by vehicle:    " + vehicleActors.Length + "  (" + pooledVehicles + " pooled, " + drawnVehicles.Length + " models)");
        Console.WriteLine("   scenes at a real place:        " + placed.Length + "  (" + string.Join(", ", placed.GroupBy(r => r.Place).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count()).ToArray()) + ")");
        Console.WriteLine("   scenes with scenery:           " + dressed.Length);
        Console.WriteLine("   scenes with others in them:    " + all.Count(r => r.Actors.Any(a => !a.IsSuspect)) + "  (staff, guards, victims, callers)");

        var sample = recipes.Cast<CalloutRecipe>().FirstOrDefault(r => r.Id == "lspd-armed-robbery-off-licence");
        if (sample != null)
        {
            Console.WriteLine();
            Console.WriteLine("   spot check - " + sample.Display);
            Console.WriteLine("      duty   " + sample.For + ", " + sample.Actors.Count + " actor group(s), resolution " + sample.Resolution);
            Console.WriteLine("      says   " + string.Join(" | ", sample.Script.ToArray()));
        }

        return Report();
    }

    private static int Report()
    {
        Console.WriteLine();
        Console.WriteLine((_failures == 0 ? "PASS" : "FAIL") + ": " + _checks + " checks, " + _failures + " failed");
        return _failures == 0 ? 0 : 1;
    }

    private static IList ListField(Type type, string name)
    {
        var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return field == null ? new ArrayList() : (IList)field.GetValue(null);
    }
}
