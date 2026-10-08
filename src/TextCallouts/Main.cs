using System;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Attributes;
using TextCallouts.Callouts;

namespace TextCallouts
{
    /// <summary>
    /// The class LSPDFR instantiates.
    ///
    /// LSPDFR does not use RAGE Plugin Hook's plugin attribute: it looks for a class named "Main"
    /// deriving from LSPD_First_Response.Mod.API.Plugin. That is why this project references LSPDFR
    /// itself at build time - deriving from a type needs it - and why the DLL must live in
    /// Plugins\LSPDFR, where LSPDFR loads it into its own AppDomain.
    /// </summary>
    public class Main : Plugin
    {
        /// <summary>
        /// The police callouts, and the five that are not police work at all.
        ///
        /// The medical ones are in the same list because LSPDFR has one callout registry and no notion
        /// of an EMS callout; each of them decides for itself whether the player's current agency is one
        /// it belongs to, and declines politely if not (see Callouts.EmsCallout).
        /// </summary>
        private static readonly Type[] CalloutTypes =
        {
            // the medical ones - offered only to an emergency-medical agency
            typeof(Callouts.CardiacArrest),
            typeof(Callouts.CollisionWithInjuries),
            typeof(Callouts.FireStandby),
            typeof(Callouts.Overdose),
            typeof(Callouts.WelfareCheck),

            // the police ones
            typeof(Callouts.ArmedRobbery),
            typeof(Callouts.BarricadedSuspect),
            typeof(Callouts.BicycleTheft),
            typeof(Callouts.BrandishingWeapon),
            typeof(Callouts.DomesticDisturbance),
            typeof(Callouts.OfficerNeedsAssistance),
            typeof(Callouts.PublicDisturbance),
            typeof(Callouts.RecklessDriver),
            typeof(Callouts.Shoplifting),
            typeof(Callouts.StolenVehicle),
            typeof(Callouts.StolenVehicleOnFoot),
            typeof(Callouts.SuspiciousPerson),
            typeof(Callouts.TrafficCollision),
        };

        private static bool _registered;
        private static string _dutyRegistered = "";

        public override void Initialize()
        {
            try
            {
                Hook();
                Game.AddConsoleCommands(new[] { typeof(ConsoleCommands) });

                Log.Line("loaded; " + CalloutTypes.Length + " callouts, none of them needing another plugin");
                Log.Line("five of them are medical and only offered to an emergency-medical agency; " +
                         "going on duty as LSFD is how you get them");
                Log.Line("log: " + Log.Path);
            }
            catch (Exception ex) { Log.Error("initialise", ex); }
        }

        /// <summary>
        /// LSPDFR calls this when the player goes **off duty**, not only at shutdown - and going off
        /// duty is the common case.
        ///
        /// This used to unsubscribe the duty handler, which meant the pack was never told about the
        /// next duty: its callouts were registered once and then never again, and from then on the
        /// pack was present in the folder and silent in the callout list. That is the bug behind "my
        /// plugins did not start". Nothing is unhooked now.
        /// </summary>
        public override void Finally()
        {
            Log.Line("LSPDFR called Finally - which happens on going off duty as well as at shutdown, " +
                     "so the duty handler stays subscribed");
        }

        /// <summary>
        /// Called when LSPDFR initialises plugins again. Hook() removes before it adds, so being
        /// called from here, from Initialize(), or from both leaves exactly one subscription.
        /// </summary>
        public override void InitializeAgain()
        {
            Hook();
        }

        private static void Hook()
        {
            try
            {
                Functions.OnOnDutyStateChanged -= OnDutyStateChanged;
                Functions.OnOnDutyStateChanged += OnDutyStateChanged;
            }
            catch (Exception ex) { Log.Error("hooking the duty handler", ex); }
        }

        private static void OnDutyStateChanged(bool onDuty)
        {
            if (!onDuty) return;

            // Every duty transition, not just the first one.
            //
            // LSPDFR rebuilds its callout registry each time the player goes on duty - which is why
            // every other pack registers its callouts here on every transition, and why a pack that
            // registers only once looks perfectly fine until the player goes off duty and back on, and
            // then has no callouts at all. That is exactly what happened: the pack registered eight
            // callouts at 18:28:59, the player went off duty at 18:29:07, and on the next duty every
            // pack re-registered while this one stayed silent.
            // And only what this duty is. The eighteen hand-written callouts used to be registered
            // whatever the player was wearing, which meant going on duty as LSFD and being sent to an
            // armed robbery - and, in the other direction, the medical ones offering themselves to a
            // police patrol. The gate is here rather than in each callout because that is also where
            // re-registration happens, so it costs nothing and cannot be forgotten by a new callout:
            // five of the eighteen are EmsCallout, and everything else is police work.
            var medical = Agency.AllowsFor(Agency.Medical);
            var police = Agency.AllowsFor(Agency.Police);

            var registered = 0;
            var forThisDuty = 0;
            foreach (var type in CalloutTypes)
            {
                // One line, one rule: a medical callout goes to a medical duty, everything else to a
                // police duty, and neither goes anywhere else.
                var isMedical = typeof(EmsCallout).IsAssignableFrom(type);
                if (isMedical ? !medical : !police) continue;

                forThisDuty++;
                try
                {
                    Functions.RegisterCallout(type);
                    registered++;
                }
                catch (Exception ex) { Log.Error("registering " + type.Name, ex); }
            }

            // Everything built from a file: the pack's own library of recipes first, then the player's
            // own from Custom. Each is a type built at run time from an XML file. A bad file is named in
            // the log and skipped; the rest still load.
            //
            // Counted apart, and named apart in the log, because "326" on its own is the number the
            // player uses to answer "did the library actually load?" - 326 of their own callouts would
            // be a very different thing from 326 that came with the pack.
            //
            // Recipes are gated by duty here as well, not only when LSPDFR offers one. A recipe for
            // another duty that is registered anyway is still picked by LSPDFR's timer and then aborts
            // in OnBeforeCalloutDisplayed - and every abort costs a whole callout interval, which is
            // how an LSPD patrol went twelve minutes without a call.
            var fromLibrary = 0;
            var otherDuty = 0;
            foreach (var type in Custom.CustomCallouts.LibraryTypes())
            {
                if (!Agency.AllowsFor(Custom.CustomCallouts.DutyOf(type))) { otherDuty++; continue; }
                try
                {
                    Functions.RegisterCallout(type);
                    fromLibrary++;
                }
                catch (Exception ex) { Log.Error("registering the library callout " + type.Name, ex); }
            }

            var ofTheirOwn = 0;
            foreach (var type in Custom.CustomCallouts.CustomTypes())
            {
                if (!Agency.AllowsFor(Custom.CustomCallouts.DutyOf(type))) { otherDuty++; continue; }
                try
                {
                    Functions.RegisterCallout(type);
                    ofTheirOwn++;
                }
                catch (Exception ex) { Log.Error("registering your own callout " + type.Name, ex); }
            }

            _registered = true;
            _dutyRegistered = registered + " built-in, " + fromLibrary + " from the library, " + ofTheirOwn +
                              " of your own (duty: " + (Agency.Current() ?? "unknown") + ")";
            Log.Line("registered for this duty: " + registered + " of " + forThisDuty + " built-in callouts, " +
                     fromLibrary + " of " + Custom.CustomCallouts.LibraryCount + " from the library, " +
                     ofTheirOwn + " of " + Custom.CustomCallouts.CustomCount + " of your own" +
                     (otherDuty > 0
                         ? " (" + otherDuty + " are another duty's work and are not registered; /calls still lists them)"
                         : "") +
                     (Custom.CustomCallouts.Problems > 0
                         ? "; " + Custom.CustomCallouts.Problems + " file(s) were skipped - see above"
                         : ""));

            Hud.Say("~g~TextCallouts~s~: " + (registered + fromLibrary + ofTheirOwn) + " callouts  (" +
                    registered + " built in, " + fromLibrary + " from the library" +
                    (ofTheirOwn > 0 ? ", " + ofTheirOwn + " of your own" : "") + ")" +
                    (Custom.CustomCallouts.Problems > 0 ? "  (" + Custom.CustomCallouts.Problems + " file(s) skipped - textcallouts.log says why.)" : ""));
        }

        /// <summary>
        /// F4 console diagnostics. There is no menu in this pack and nothing to click, so when a
        /// callout does not turn up this is how to find out why.
        /// </summary>
        public static class ConsoleCommands
        {
            [ConsoleCommand("tcstatus", Description = "TextCallouts: what is registered, the library, your own callouts, and where the log is.")]
            public static void Status()
            {
                Game.Console.Print("[TextCallouts] " + CalloutTypes.Length + " callouts in the pack, re-registered on");
                Game.Console.Print("[TextCallouts]   every duty transition" +
                                   (_registered ? " (last one done)." : " - not registered yet, so go on duty."));
                if (_registered && _dutyRegistered.Length > 0)
                    Game.Console.Print("[TextCallouts] registered " + _dutyRegistered);

                Game.Console.Print("[TextCallouts] " + Custom.CustomCallouts.LibraryCount +
                                   " more from the library that ships with it");
                Game.Console.Print("[TextCallouts] " + Custom.CustomCallouts.CustomCount + " from your own files" +
                                   (Custom.CustomCallouts.Problems > 0
                                       ? ", and " + Custom.CustomCallouts.Problems + " file(s) skipped"
                                       : ""));
                Game.Console.Print("[TextCallouts] library: " + Custom.CustomCallouts.LibraryFolder);
                Game.Console.Print("[TextCallouts] your folder: " + Custom.CustomCallouts.Folder);
                Game.Console.Print("[TextCallouts] log: " + Log.Path);
            }

            [ConsoleCommand("tccallouts", Description = "TextCallouts: list what this pack provides, including your own.")]
            public static void List()
            {
                Game.Console.Print("[TextCallouts] in the pack, written in C#:");
                foreach (var type in CalloutTypes) Print(type);

                Game.Console.Print("[TextCallouts] the library that ships with it (" + Custom.CustomCallouts.LibraryFolder + "):");
                var library = Custom.CustomCallouts.LibraryTypes();
                if (library.Length == 0)
                {
                    Game.Console.Print("   none - the Library folder is missing from Plugins\\LSPDFR\\TextCallouts\\.");
                    Game.Console.Print("   Copy it in from the download and go on duty again.");
                }
                foreach (var type in library) Print(type);

                Game.Console.Print("[TextCallouts] yours (" + Custom.CustomCallouts.Folder + "):");
                var custom = Custom.CustomCallouts.CustomTypes();
                if (custom.Length == 0) Game.Console.Print("   none yet - put an .xml file in that folder");
                foreach (var type in custom) Print(type);
            }

            private static void Print(Type type)
            {
                var name = type.Name;
                try
                {
                    var attribute = (CalloutInfoAttribute)Attribute.GetCustomAttribute(type, typeof(CalloutInfoAttribute));
                    if (attribute != null) name = attribute.Name;
                }
                catch { }

                Game.Console.Print("   " + name);
                Log.Line("callout: " + name);
            }
        }
    }
}
