using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A robbery where the suspect is still there and still armed. This one is hostile from the
    /// start - the only question is whether the player gets close enough to talk before it turns
    /// into a shooting.
    /// </summary>
    [CalloutInfo("Armed Robbery In Progress", CalloutProbability.Medium)]
    public class ArmedRobbery : TextCallout
    {
        private static readonly string[] Suspects =
        {
            "a_m_y_musclbeac_01", "a_m_m_soucent_01", "a_m_y_stlat_01", "g_m_y_lost_01"
        };

        private bool _engaged;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Armed Robbery In Progress",
                  "Armed robbery in progress, suspect still on scene.",
                  "Male suspect, handgun, staff and customers inside.",
                  StreetNear(player.Position, 150f, 300f),
                  40f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get
            {
                return "He is armed and he is still there. Do not go in alone - ask for backup and " +
                       "take it slowly.";
            }
        }

        protected override void Build()
        {
            Suspect = SpawnPed(Suspects[Rng.Next(Suspects.Length)], CalloutPosition, Rng.Next(360));
            MakeHostile(Suspect, "WEAPON_PISTOL", 120);
            MarkStoppable(Suspect);

            Say("Suspect is outside at " + Where(CalloutPosition) + ", he has a handgun.");
            Hud.Help("Call for backup before you close in.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect)) { Close("the armed suspect is down or in custody"); return false; }

            if (!_engaged && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 30f)
            {
                _engaged = true;
                Say("He has seen you. He is not putting it down.");
            }

            return true;
        }
    }
}
