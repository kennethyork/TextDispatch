namespace TextDispatch
{
    /// <summary>
    /// The entry point LSPDFR looks for.
    ///
    /// LSPDFR does not use RAGE Plugin Hook's plugin attribute for the plugins it loads out of
    /// Plugins\LSPDFR. What it does is:
    ///
    ///   1. look for a public class named "Main" in the plugin assembly,
    ///   2. instantiate it, and
    ///   3. call Initialize() - and Finally() when the plugin unloads.
    ///
    /// The class must therefore derive from LSPD_First_Response.Mod.API.Plugin, which is abstract
    /// with an abstract Initialize() and Finally(). Every working plugin in that folder has exactly
    /// this shape, which is how it was established.
    ///
    /// Being loaded by LSPDFR is not a preference. It is the only way to run inside LSPDFR's
    /// AppDomain, and a plugin loadable by RPH gets its own AppDomain where LSPDFR's types do not
    /// exist at all - the plugin runs, and every call into LSPDFR fails silently.
    ///
    /// The base type is written out in full because this namespace also has a class called Plugin,
    /// which is the engine this one drives.
    /// </summary>
    public class Main : LSPD_First_Response.Mod.API.Plugin
    {
        public override void Initialize()
        {
            TextDispatch.Plugin.Start();
        }

        public override void Finally()
        {
            TextDispatch.Plugin.Stop();
        }

        /// <summary>
        /// LSPDFR calls this when it reloads its own configuration. There is nothing to rebuild:
        /// the engine reads what it needs every tick.
        /// </summary>
        public override void InitializeAgain()
        {
        }
    }
}
