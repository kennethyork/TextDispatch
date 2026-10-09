using System;
using LSPD_First_Response;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A car burning in a car park, one person who breathed too much of it, and an engine that is not
    /// there yet. The player's job is the person and the onlookers; the fire is the fire brigade's.
    ///
    /// Offered only while the player is on duty as an emergency-medical agency (see EmsCallout). The
    /// engine is asked for through LSPDFR's own backup system, the same way the ambulances are.
    /// </summary>
    [CalloutInfo("Vehicle Fire", CalloutProbability.Medium)]
    public class FireStandby : EmsCallout
    {
        private static readonly string[] Cars = { "premier", "asea", "ingot", "glendale", "stanier", "emperor" };
        private static readonly string[] People =
        {
            "a_m_y_business_01", "a_f_y_business_01", "a_m_m_bevhills_01", "a_f_y_vinewood_01"
        };

        private Vehicle _burning;
        private Ped _onlooker;
        private bool _fireLit;
        private bool _fireCleared;

        public override bool OnBeforeCalloutDisplayed()
        {
            if (!OfferedToThisAgency()) return false;

            var player = Game.LocalPlayer.Character;
            Offer("Vehicle Fire",
                  "Vehicle on fire in a car park, one person overcome by smoke.",
                  "Fire brigade not on scene. Keep everyone back from it.",
                  StreetNear(player.Position, 150f, 300f),
                  40f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "Somebody has taken smoke in. Check them, and keep the rest of them away from the car until the engine gets here."; }
        }

        protected override string SignOff
        {
            get { return "The engine has the fire, and the casualty is with the medics. 10-8."; }
        }

        protected override string Working { get { return "Check the casualty who breathed the smoke."; } }

        protected override int TreatmentSeconds { get { return 10; } }

        protected override int HandoverSeconds { get { return 20; } }

        protected override string Stable
        {
            get { return "Get the casualty away from the smoke and give them air. Engine is two minutes out."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            // The car: fire-damaged, dead, and lit. A native, because there is no managed fire in RPH.
            _burning = SpawnVehicle(Cars[Rng.Next(Cars.Length)], position, Rng.Next(360));
            try { _burning.EngineHealth = 0f; } catch { }
            try { _burning.IsEngineOn = false; } catch { }

            try
            {
                Rage.Native.NativeFunction.Natives.StartScriptFire(_burning.Position.X, _burning.Position.Y,
                                                                   _burning.Position.Z, 1, true);
                _fireLit = true;
            }
            catch (Exception ex) { Log.Error("lighting the fire", ex); }

            Patient = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(6f, 3f, 0f), Rng.Next(360));
            Hurt(Patient, 70);
            Collapse(Patient);

            _onlooker = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(8f, 4f, 0f), Rng.Next(360));
            try { _onlooker.Tasks.StandStill(-1); } catch { }

            Say("Vehicle well alight at " + Where(position) + ". One person has been near the smoke, engine is on the way.");
        }

        protected override void Arrived()
        {
            Say("Onlooker: it just went up. Nobody was in it, but he was standing right next to it when it started.");
        }

        protected override void Treated()
        {
            ClearFire();
            Say("Casualty is off the smoke and breathing easier.");
        }

        /// <summary>The engine, not an ambulance: this is the one scene that needs water.</summary>
        protected override void SummonBackup()
        {
            try
            {
                Functions.RequestBackup(CalloutPosition, EBackupResponseType.Code3, EBackupUnitType.Firetruck);
                Log.Line("fire brigade requested for " + FriendlyName);
            }
            catch (Exception ex) { Log.Error("requesting the fire brigade", ex); }
        }

        protected override bool Tick()
        {
            var keepGoing = base.Tick();

            // A script fire is not an entity, so nothing deletes it with the rest of the scene. Cleared
            // when the casualty is seen to, and again if the player has gone somewhere else entirely -
            // leaving a fire burning in a car park after the call has moved on is not a small bug.
            if (!_fireCleared)
            {
                var away = false;
                try { away = Game.LocalPlayer.Character.Position.DistanceTo(CalloutPosition) > 250f; } catch { }
                if (away) ClearFire();
            }

            return keepGoing;
        }

        private void ClearFire()
        {
            if (!_fireLit || _fireCleared) return;
            _fireCleared = true;

            try
            {
                var position = _burning != null && _burning.Exists() ? _burning.Position : CalloutPosition;
                Rage.Native.NativeFunction.Natives.StopFireInRange(position.X, position.Y, position.Z, 25f);
                Log.Line("the fire was put out at " + Where(position));
            }
            catch (Exception ex) { Log.Error("putting the fire out", ex); }
        }
    }
}
