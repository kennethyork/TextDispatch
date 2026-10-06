using System;
using System.Collections.Generic;

namespace TextCallouts.Custom
{
    /// <summary>
    /// Stands in for the real RecipeCallout, and for one reason: the real one cannot be loaded outside
    /// the game. It derives from LSPD_First_Response.Mod.Callouts.Callout and its signatures mention
    /// Rage types, and the only RagePluginHook assembly on a development machine is the SDK reference
    /// assembly, whose members have no implementation - so the CLR refuses to load anything that
    /// touches it.
    ///
    /// This is the entire surface the loader uses of it: one Bind call when a type has been built, and
    /// one typeof as the base class to build it against. In game it is the real class; the loader is
    /// the same file either way, and the loader is what this check is for.
    /// </summary>
    public class RecipeCallout
    {
        /// <summary>Type name to the recipe it came from, exactly as the real one keys it.</summary>
        internal static readonly Dictionary<string, CalloutRecipe> Bound =
            new Dictionary<string, CalloutRecipe>(StringComparer.OrdinalIgnoreCase);

        internal static void Bind(string typeName, CalloutRecipe recipe)
        {
            if (string.IsNullOrEmpty(typeName) || recipe == null) return;
            Bound[typeName] = recipe;
        }
    }
}
