using System;
using LSPD_First_Response;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// Shots fired, and a unit already on scene asking for help. Two armed suspects, and LSPDFR's own
    /// backup system bringing the cavalry - this is the one callout in the pack that is meant to be
    /// hard on purpose.
    /// </summary>
    [CalloutInfo("Officer Needs Assistance", CalloutProbability.Low)]
    public class OfficerNeedsAssistance : TextCallout
    {
        private static readonly string[] Suspects = { "g_m_y_lost_01", "g_m_y_mexgang_01", "a_m_y_musclbeac_01" };

        private Ped _second;
        private bool _backupCalled;
        private bool _engaged;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Officer Needs Assistance",
                  "Shots fired - officer on scene requesting immediate assistance.",
                  "Two armed suspects, officer is behind cover.",
                  StreetNear(player.Position, 200f, 380f),
                  50f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get
            {
                return "Shots fired. Two armed suspects. Get there, get backup rolling, and do not walk " +
                       "into the open.";
            }
        }

        protected override string SignOff
        {
            get { return "Scene secure. That was a good job."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            Suspect = SpawnPed(Suspects[Rng.Next(Suspects.Length)], position, Rng.Next(360));
            _second = SpawnPed(Suspects[Rng.Next(Suspects.Length)], position + new Vector3(2.5f, 1f, 0f), Rng.Next(360));

            MakeHostile(Suspect, "WEAPON_PISTOL", 200);
            MakeHostile(_second, "WEAPON_PISTOL", 200);
            MarkStoppable(Suspect);
            MarkStoppable(_second);

            Say("Shots fired at " + Where(position) + ". Two suspects, both armed.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect) && Dealt(_second)) { Close("both suspects are down or in custody"); return false; }

            // Ask for help the moment the player is close enough for it to matter, not before: a unit
            // that arrives to an empty street helps nobody.
            if (!_backupCalled && Game.LocalPlayer.Character.Position.DistanceTo(CalloutPosition) < 120f)
            {
                _backupCalled = true;
                try
                {
                    Functions.RequestBackup(CalloutPosition, EBackupResponseType.Code3, EBackupUnitType.LocalUnit);
                    Log.Line("backup requested for " + FriendlyName);
                }
                catch (Exception ex) { Log.Error("requesting backup", ex); }

                Say("Two units rolling code 3 to your location.");
            }

            if (!_engaged && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 40f)
            {
                _engaged = true;
                Say("They have seen you. Look out.");
                Hud.Help("Use the car as cover until backup arrives.");
            }

            return true;
        }
    }
}
