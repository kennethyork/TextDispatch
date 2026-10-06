using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

/// <summary>
/// Checks the names TextDispatch looks up by reflection in TextCallouts.
///
/// The two plugins find each other at run time, by name, inside LSPDFR's AppDomain - which is what lets
/// either work without the other, and also what makes a rename on either side a silent failure in game:
/// the callout's people would simply stop answering, and nothing would say why. The compiler cannot see
/// across that boundary, so this does.
/// </summary>
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
        var repo = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

        var packDll = Path.Combine(repo, @"src\TextCallouts\bin\Release\TextCallouts.dll");
        var dispatchDll = Path.Combine(repo, @"src\TextDispatch\bin\Release\TextDispatch.dll");
        var jobsDll = Path.Combine(repo, @"src\TextJobs\bin\Release\TextJobs.dll");

        Console.WriteLine("the bridge between the two plugins, read from their metadata:");
        Console.WriteLine();

        foreach (var path in new[] { packDll, dispatchDll, jobsDll })
        {
            if (File.Exists(path)) continue;
            Console.WriteLine("  the assemblies are not built yet: " + path);
            return 1;
        }

        var pack = AssemblyDefinition.ReadAssembly(packDll);
        var dispatch = AssemblyDefinition.ReadAssembly(dispatchDll);
        var jobs = AssemblyDefinition.ReadAssembly(jobsDll);

        // --- what the callout pack publishes
        var registry = pack.MainModule.GetType("TextCallouts.Scripts.CalloutScript");
        Check("TextCallouts publishes TextCallouts.Scripts.CalloutScript", registry != null);

        if (registry != null)
        {
            var methods = registry.Methods.Where(m => m.IsPublic && m.IsStatic).Select(m => m.Name).ToArray();
            foreach (var needed in new[] { "Register", "Reply", "Has", "NameOf", "Forget", "Clear" })
                Check("  with " + needed + " on it", methods.Contains(needed));

            var reply = registry.Methods.FirstOrDefault(m => m.Name == "Reply");
            Check("  Reply takes a ped and what was said", reply != null && reply.Parameters.Count == 2);
        }

        // --- and what TextDispatch looks for
        var bridge = dispatch.MainModule.GetType("TextDispatch.Bridges.CalloutScriptBridge");
        Check("TextDispatch publishes TextDispatch.Bridges.CalloutScriptBridge", bridge != null);

        if (bridge != null)
        {
            var methods = bridge.Methods.Where(m => m.IsPublic).Select(m => m.Name).ToArray();
            Check("  which can ask whether a person is scripted", methods.Contains("Reply"));
            Check("  and read their name", methods.Contains("NameOf"));

            // The name it looks for has to match character for character - that is the whole point.
            var literal = bridge.Fields.Where(f => f.Constant != null).Select(f => f.Constant.ToString()).FirstOrDefault();
            Check("  and it looks for the name '" + literal + "'", literal == "TextCallouts.Scripts.CalloutScript");
        }

        // --- the second bridge: the two chat boxes and the one key they share
        //
        // The jobs box uses the left arrow unless the police box is loaded, and the only way it can
        // tell is a file TextDispatch refreshes while it runs. The name is a constant in each, which
        // is why it can be read here and why it has to be read: a rename on either side would not
        // break a build, or a test, or anything until a player pressed the key and nothing opened.
        var alive = dispatch.MainModule.GetType("TextDispatch.Plugin");
        var expected = jobs.MainModule.GetType("TextJobs.Settings");

        var dispatchName = alive == null ? null
            : alive.Fields.Where(f => f.Name == "AliveFile" && f.Constant != null)
                          .Select(f => f.Constant.ToString()).FirstOrDefault();
        var jobsName = expected == null ? null
            : expected.Fields.Where(f => f.Name == "PoliceAliveFile" && f.Constant != null)
                             .Select(f => f.Constant.ToString()).FirstOrDefault();

        Check("TextDispatch says which file announces it", !string.IsNullOrEmpty(dispatchName));
        Check("TextJobs looks for the same file", !string.IsNullOrEmpty(jobsName) && jobsName == dispatchName);
        Check("  and neither is empty", (dispatchName ?? "").Length > 0);

        // --- and that the recipe engine can express both features, since the notes promise them
        var recipe = pack.MainModule.GetType("TextCallouts.Custom.CalloutRecipe");
        var stage = pack.MainModule.GetType("TextCallouts.Custom.StageRecipe");
        Check("a recipe can carry what its people say", recipe != null && recipe.Fields.Any(f => f.Name == "Script"));
        Check("a recipe can carry a sequence of things that happen", recipe != null && recipe.Fields.Any(f => f.Name == "Stages"));
        Check("and a stage knows when and what it does", stage != null &&
              stage.Fields.Any(f => f.Name == "At") && stage.Fields.Any(f => f.Name == "Do"));

        Console.WriteLine();
        Console.WriteLine((_failures == 0 ? "PASS" : "FAIL") + ": " + _checks + " checks, " + _failures + " failed");
        return _failures == 0 ? 0 : 1;
    }
}
