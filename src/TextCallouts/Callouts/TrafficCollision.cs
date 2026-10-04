using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// Nobody is a suspect here: two cars, one injured driver, and a patient who needs an ambulance
    /// more than they need a ticket. It is in the pack on purpose - a callout pack that is only ever
    /// chases and shootings is not what a patrol feels like.
    ///
    /// The ambulance is requested through LSPDFR's own backup system, so nothing else is needed.
    /// </summary>
    [CalloutInfo("Two-Car Collision", CalloutProbability.Medium)]
    public class TrafficCollision : TextCallout
    {
        private static readonly string[] Cars = { "premier", "asea", "ingot", "glendale", "stanier" };
        private static readonly string[] Civilians =
        {
            "a_m_y_business_01", "a_f_y_business_01", "a_m_y_vinewood_01", "a_m_m_business_01"
        };

        private Ped _other;
        private int _requestedAt;
        private bool _ambulanceRequested;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Two-Car Collision",
                  "Two-vehicle collision, one driver reported injured.",
                  "Both vehicles still on scene, one person still inside.",
                  StreetNear(player.Position, 150f, 300f),
                  40f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "One driver is hurt. Get him out and get an ambulance rolling - this one is not about arrests."; }
        }

        protected override string SignOff
        {
            get { return "Fire and EMS are handling it from here. Show me 10-8."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            // Vehicle one, with the injured driver still in the seat.
            SuspectVehicle = SpawnVehicle(Cars[Rng.Next(Cars.Length)], position, Rng.Next(360));
            SuspectVehicle.EngineHealth = 120f;
            Suspect = SpawnPed(Civilians[Rng.Next(Civilians.Length)], position, Rng.Next(360));
            PutInVehicle(Suspect, SuspectVehicle, -1);
            Suspect.Health = 90;                    // hurt, not dying
            MarkStoppable(Suspect);

            // The other car, a couple of metres away, driver out and complaining.
            _other = SpawnPed(Civilians[Rng.Next(Civilians.Length)],
                              position + new Vector3(4.5f, 1.5f, 0f), Rng.Next(360));
            var second = SpawnVehicle(Cars[Rng.Next(Cars.Length)], position + new Vector3(5f, 2f, 0f), Rng.Next(360));
            second.EngineHealth = 300f;
            _other.Tasks.StandStill(-1);
            MarkStoppable(_other);

            Say("Two vehicles blocking the road at " + Where(position) + ". One driver is still inside.");
        }

        protected override bool Tick()
        {
            if (!_ambulanceRequested)
            {
                var distance = Game.LocalPlayer.Character.Position.DistanceTo(CalloutPosition);
                if (distance < 18f)
                {
                    _ambulanceRequested = true;
                    _requestedAt = Environment.TickCount;
                    Say("You are on scene. Ambulance is rolling from Pillbox.");
                    RequestAmbulance(CalloutPosition);
                    Hud.Help("EMS is on the way - keep the lane clear.");
                }
            }
            else if (Environment.TickCount - _requestedAt > 150000)
            {
                // Three minutes is long enough for anyone to wait for an ambulance in a video game.
                Close("EMS took over the scene");
                return false;
            }

            return true;
        }
    }
}
