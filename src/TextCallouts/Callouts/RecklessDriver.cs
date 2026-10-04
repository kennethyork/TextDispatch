using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A car being driven badly. Nothing is criminal until the driver decides it is: pull alongside
    /// and they run, which becomes a pursuit.
    /// </summary>
    [CalloutInfo("Reckless Driver", CalloutProbability.High)]
    public class RecklessDriver : TextCallout
    {
        private static readonly string[] Cars = { "buffalo", "feltzer2", "sultan", "schafter2", "penumbra" };
        private static readonly string[] Drivers = { "a_m_y_hipster_01", "a_m_y_vinewood_01", "a_m_y_musclbeac_02" };

        private bool _running;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Reckless Driver",
                  "Multiple callers reporting a vehicle being driven dangerously.",
                  "Last seen weaving through traffic.",
                  StreetNear(player.Position, 150f, 320f),
                  45f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "Callers say he is all over the road. Find him and see what he does when he sees you."; }
        }

        protected override void Build()
        {
            SuspectVehicle = SpawnVehicle(Cars[Rng.Next(Cars.Length)], CalloutPosition, Rng.Next(360));
            Suspect = SpawnPed(Drivers[Rng.Next(Drivers.Length)], CalloutPosition, Rng.Next(360));
            PutInVehicle(Suspect, SuspectVehicle, -1);
            MarkStoppable(Suspect);

            Say("He is in a " + DescribeCar(SuspectVehicle) + " near " + Where(CalloutPosition) + ".");
        }

        protected override bool Tick()
        {
            if (SuspectVehicle == null || !SuspectVehicle.Exists()) { Close("the vehicle is gone"); return false; }

            if (!_running && Game.LocalPlayer.Character.Position.DistanceTo(SuspectVehicle.Position) < 30f)
            {
                _running = true;
                Say("He has seen you and he is running. 10-80.");
                FleeInVehicle(Suspect);
            }

            if (Dealt(Suspect)) { Close("the driver is in custody"); return false; }
            return true;
        }

        private static string DescribeCar(Vehicle vehicle)
        {
            try { return vehicle.Model.Name; }
            catch { return "vehicle"; }
        }
    }
}
