using System;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Attributes;

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
        private static readonly Type[] CalloutTypes =
        {
            typeof(Callouts.OfficerNeedsAssistance),
            typeof(Callouts.StolenVehicle),
            typeof(Callouts.RecklessDriver),
            typeof(Callouts.DomesticDisturbance),
            typeof(Callouts.Shoplifting),
            typeof(Callouts.ArmedRobbery),
            typeof(Callouts.TrafficCollision),
            typeof(Callouts.SuspiciousPerson),
        };

        private static bool _registered;

        public override void Initialize()
        {
            try
            {
                Functions.OnOnDutyStateChanged += OnDutyStateChanged;
                Game.AddConsoleCommands(new[] { typeof(ConsoleCommands) });

                Log.Line("loaded; " + CalloutTypes.Length + " callouts, none of them needing another plugin");
                Log.Line("log: " + Log.Path);
            }
            catch (Exception ex) { Log.Error("initialise", ex); }
        }

        public override void Finally()
        {
            try { Functions.OnOnDutyStateChanged -= OnDutyStateChanged; }
            catch { }

            Log.Line("unloaded");
        }

        private static void OnDutyStateChanged(bool onDuty)
        {
            if (!onDuty) return;
            if (_registered) return;

            var registered = 0;
            foreach (var type in CalloutTypes)
            {
                try
                {
                    Functions.RegisterCallout(type);
                    registered++;
                }
                catch (Exception ex) { Log.Error("registering " + type.Name, ex); }
            }

            _registered = true;
            Log.Line("registered " + registered + " of " + CalloutTypes.Length + " callouts");
            Hud.Say("~g~TextCallouts~s~: " + registered + " callouts available.");
        }

        /// <summary>
        /// F4 console diagnostics. There is no menu in this pack and nothing to click, so when a
        /// callout does not turn up this is how to find out why.
        /// </summary>
        public static class ConsoleCommands
        {
            [ConsoleCommand("tcstatus", Description = "TextCallouts: how many callouts are registered, and where the log is.")]
            public static void Status()
            {
                Game.Console.Print("[TextCallouts] " + CalloutTypes.Length + " callouts in the pack" +
                                   (_registered ? ", registered." : ", NOT registered yet - go on duty."));

                foreach (var type in CalloutTypes) Game.Console.Print("   " + type.Name);
                Game.Console.Print("[TextCallouts] log: " + Log.Path);
            }

            [ConsoleCommand("tccallouts", Description = "TextCallouts: list what this pack provides.")]
            public static void List()
            {
                foreach (var type in CalloutTypes)
                {
                    var name = type.Name;
                    try
                    {
                        var attribute = (CalloutInfoAttribute)Attribute.GetCustomAttribute(type, typeof(CalloutInfoAttribute));
                        if (attribute != null) name = attribute.Name;
                    }
                    catch { }

                    Game.Console.Print("   " + name + "   (" + type.Name + ")");
                    Log.Line("callout: " + name);
                }
            }
        }
    }
}
