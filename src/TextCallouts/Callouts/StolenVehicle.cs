using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// An occupied stolen vehicle. Dispatch puts it on the air, the driver does not stop, and the
    /// chase is LSPDFR's own pursuit AI - the pack only has to notice when it is over.
    /// </summary>
    [CalloutInfo("Occupied Stolen Vehicle", CalloutProbability.Medium)]
    public class StolenVehicle : TextCallout
    {
        private static readonly string[] Cars = { "sultan", "blista", "premier", "asea", "primo", "sentinel" };
        private static readonly string[] Drivers =
        {
            "a_m_y_business_01", "a_m_y_hipster_01", "a_m_m_business_01", "a_m_y_soucent_01", "a_m_y_stlat_01"
        };

        private bool _running;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Occupied Stolen Vehicle",
                  "An occupied stolen vehicle is moving in your area.",
                  "Registered owner has reported it taken this evening.",
                  StreetNear(player.Position, 180f, 350f),
                  45f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "Show me 10-97 on that stolen vehicle. Traffic stop if you can, pursuit if you have to."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;
            SuspectVehicle = SpawnVehicle(Cars[Rng.Next(Cars.Length)], position, Rng.Next(360));
            SuspectVehicle.IsStolen = true;

            Suspect = SpawnPed(Drivers[Rng.Next(Drivers.Length)], position, Rng.Next(360));
            PutInVehicle(Suspect, SuspectVehicle, -1);
            MarkStoppable(Suspect);

            Say("Be advised, the vehicle is showing as stolen out of " + Where(position) + ".");
        }

        protected override bool Tick()
        {
            if (SuspectVehicle == null || !SuspectVehicle.Exists()) { Close("the vehicle is gone"); return false; }

            // Once the player is close enough to attempt a stop, the driver runs - on wheels, driven
            // by LSPDFR's own pursuit AI.
            if (!_running && Game.LocalPlayer.Character.Position.DistanceTo(SuspectVehicle.Position) < 35f)
            {
                _running = true;
                Say("He is not stopping. 10-80, he is running - the pursuit is mine.");
                FleeInVehicle(Suspect);
                Hud.Help("Keep with the vehicle - it is yours once it stops.");
            }

            if (Dealt(Suspect)) { Close("the driver is in custody"); return false; }
            return true;
        }
    }
}
