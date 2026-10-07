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

        /// <summary>
        /// LSPDFR calls this when the player goes **off duty**, and again at shutdown - and it does
        /// not call Initialize() when they go back on duty.
        ///
        /// This used to tear the engine down, which was fatal to the rest of the session: the fiber
        /// stopped, rendering was unhooked, and the chat box was gone from the moment the player went
        /// off duty until the game was restarted. The log proved it - "stopping" at 18:29:07 when the
        /// player went off duty, and nothing after it, while packs that ignore this callback carried
        /// on working.
        ///
        /// So nothing is torn down. Going off duty is a state, not a shutdown: the engine keeps
        /// running and the services already read the player's duty state from LSPDFR every tick, so
        /// they go quiet on their own.
        /// </summary>
        public override void Finally()
        {
            // The other chat box has to hear this, and this is the only moment LSPDFR offers for it:
            // the jobs box stands down while the player is on duty, and comes back - on its own key -
            // when they are not.
            TextDispatch.Plugin.SetOnDuty(false);

            Log.Line("LSPDFR called Finally - which happens on going off duty as well as at shutdown, " +
                     "so the engine is deliberately left running");
        }

        /// <summary>
        /// Called when LSPDFR initialises plugins again. Start() returns immediately if the engine is
        /// already up, so this is safe whether or not Finally() turned out to mean "off duty".
        /// </summary>
        public override void InitializeAgain()
        {
            TextDispatch.Plugin.Start();
        }
    }
}
