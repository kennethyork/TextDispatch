using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A bike taken from outside a shop, and a suspect who rides rather than runs.
    ///
    /// Short and sharp: they are fast, they are on two wheels, and LSPDFR's pursuit AI does the
    /// driving. Worth having because a bicycle chase plays completely differently from a car one -
    /// you can stay with them on foot across a car park but not across the city.
    /// </summary>
    [CalloutInfo("Bicycle Theft", CalloutProbability.High)]
    public class BicycleTheft : TextCallout
    {
        private static readonly string[] Bikes = { "bmx", "scorcher", "fixter", "cruiser" };
        private static readonly string[] Riders =
        {
            "a_m_y_hipster_01", "a_m_y_vinewood_01", "a_m_y_stlat_01", "a_m_y_soucent_01"
        };

        private bool _moving;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Bicycle Theft",
                  "Bicycle taken from outside a shop, suspect still in sight.",
                  "Riding away on the stolen bike, heading up the street.",
                  StreetNear(player.Position, 120f, 240f),
                  30f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "He is on the bike and he is quick. You will not catch it on foot - take the car."; }
        }

        protected override void Build()
        {
            SuspectVehicle = SpawnVehicle(Bikes[Rng.Next(Bikes.Length)], CalloutPosition, Rng.Next(360));
            Suspect = SpawnPed(Riders[Rng.Next(Riders.Length)], CalloutPosition, Rng.Next(360));
            PutInVehicle(Suspect, SuspectVehicle, -1);
            MarkStoppable(Suspect);

            Say("He is on the bike at " + Where(CalloutPosition) + " and moving.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect)) { Close("the suspect is in custody"); return false; }

            if (SuspectVehicle == null || !SuspectVehicle.Exists()) { Close("the bike is gone"); return false; }

            if (!_moving && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 40f)
            {
                _moving = true;
                Say("He has seen you and he is riding. 10-80 as soon as you are behind him.");
                FleeInVehicle(Suspect);
                Hud.Help("Stay behind him - the pursuit is yours once he stops.");
            }

            return true;
        }
    }
}
