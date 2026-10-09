using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A crash with somebody still sitting in a wrecked car. The police version of this scene is in the
    /// pack already and is about keeping the road open; this one is about getting the door open.
    ///
    /// Offered only while the player is on duty as an emergency-medical agency (see EmsCallout).
    /// </summary>
    [CalloutInfo("Collision with Injuries", CalloutProbability.Medium)]
    public class CollisionWithInjuries : EmsCallout
    {
        private static readonly string[] Cars = { "premier", "asea", "ingot", "glendale", "stanier", "asterope" };
        private static readonly string[] People =
        {
            "a_m_y_business_01", "a_f_y_business_01", "a_m_y_vinewood_01", "a_m_m_hasjew_01"
        };

        private Ped _walkingWounded;
        private bool _pulledOut;

        public override bool OnBeforeCalloutDisplayed()
        {
            if (!OfferedToThisAgency()) return false;

            var player = Game.LocalPlayer.Character;
            Offer("Collision with Injuries",
                  "Two-vehicle collision, one person trapped.",
                  "One casualty still in the vehicle, one walking wounded.",
                  StreetNear(player.Position, 150f, 300f),
                  40f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "One of them is still in the car. Get them out, then treat whoever is worse."; }
        }

        protected override string SignOff
        {
            get { return "Both of them are with the medics. Nothing for you here any more - 10-8."; }
        }

        protected override string Working { get { return "Treat the casualty - the ambulance is on its way to the scene."; } }

        protected override int TreatmentSeconds { get { return 15; } }

        protected override string Stable
        {
            get { return "Both casualties are stable. Ambulance is rolling to your location."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            // The casualty in the car: a real door, a real seat, and somebody who cannot get out alone.
            SuspectVehicle = SpawnVehicle(Cars[Rng.Next(Cars.Length)], position, Rng.Next(360));
            SuspectVehicle.EngineHealth = 90f;
            Patient = SpawnPed(People[Rng.Next(People.Length)], position, Rng.Next(360));
            PutInVehicle(Patient, SuspectVehicle, -1);
            Hurt(Patient, 75);

            // The other driver, out of the car and on their feet, which is how most of these look.
            _walkingWounded = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(4f, 2f, 0f), Rng.Next(360));
            try { _walkingWounded.Tasks.StandStill(-1); } catch { }

            Say("Two vehicles in the road at " + Where(position) + ". One casualty is still inside.");
        }

        /// <summary>
        /// Getting the casualty out is the first thing the player does, so it happens when they arrive
        /// rather than when they ask. A native task, because a managed one will not make a hurt ped
        /// leave a car.
        /// </summary>
        protected override void Arrived()
        {
            if (_pulledOut || Patient == null || !Patient.Exists()) return;
            if (SuspectVehicle == null || !SuspectVehicle.Exists()) return;
            _pulledOut = true;

            try
            {
                Rage.Native.NativeFunction.Natives.TaskLeaveVehicle(Patient, SuspectVehicle, 0);
                Log.Line("casualty pulled from the vehicle at " + Where(CalloutPosition));
            }
            catch (Exception ex) { Log.Error("getting a casualty out of a car", ex); }

            Collapse(Patient);

            Say("You get the door open. He is out, barely conscious, and still in the road.");
            try { Hud.Help("Get the casualty clear of the wreck and work on them there."); } catch { }
        }
    }
}
