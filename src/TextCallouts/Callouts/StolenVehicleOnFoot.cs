using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A stolen car found parked up - and the driver still in it.
    ///
    /// Different from the moving stolen vehicle callout on purpose: this one is a stop that turns into
    /// a foot chase, because the driver bails rather than driving off. The car stays where it is, so
    /// the scene is a car, a runner, and a decision about which one matters.
    /// </summary>
    [CalloutInfo("Stolen Vehicle, Driver On Foot", CalloutProbability.Medium)]
    public class StolenVehicleOnFoot : TextCallout
    {
        private static readonly string[] Cars = { "asea", "premier", "ingot", "glendale", "stanier" };
        private static readonly string[] Drivers =
        {
            "a_m_y_stlat_01", "a_m_y_soucent_01", "a_m_y_hipster_01", "a_m_m_soucent_01"
        };

        private bool _bailed;
        private bool _surrenders;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Stolen Vehicle, Driver On Foot",
                  "Stolen vehicle located, driver still with it.",
                  "Registered stolen this morning. One occupant.",
                  StreetNear(player.Position, 140f, 280f),
                  35f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "That is the stolen vehicle. He is still with it - approach it like he might run."; }
        }

        protected override void Build()
        {
            SuspectVehicle = SpawnVehicle(Cars[Rng.Next(Cars.Length)], CalloutPosition, Rng.Next(360));
            SuspectVehicle.IsStolen = true;
            SuspectVehicle.EngineHealth = 400f;

            Suspect = SpawnPed(Drivers[Rng.Next(Drivers.Length)], CalloutPosition, Rng.Next(360));
            PutInVehicle(Suspect, SuspectVehicle, -1);
            MarkStoppable(Suspect);

            _surrenders = Rng.Next(100) < 40;

            Say("Vehicle is at " + Where(CalloutPosition) + ", driver is still in the seat.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect)) { Close("the driver is in custody"); return false; }

            if (!_bailed && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 30f)
            {
                _bailed = true;

                if (_surrenders)
                {
                    Say("He is staying put. Take it slowly and you will have him.");
                    Surrender(Suspect);
                }
                else
                {
                    Say("He is out of the car and running on foot. He is not taking it to a pursuit.");
                    try { Suspect.Tasks.LeaveVehicle(SuspectVehicle, 0); }
                    catch (Exception ex) { Log.Error("LeaveVehicle", ex); }
                    FleeOnFoot(Suspect);
                    Hud.Help("Leave the car - he is on foot and he is quick.");
                }
            }

            return true;
        }
    }
}
