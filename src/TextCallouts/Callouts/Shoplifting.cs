using LSPDFR = LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A shopkeeper reports someone walking out with goods. On foot, and it usually runs - which is
    /// a foot pursuit the player has to win rather than a chase the game drives for them.
    /// </summary>
    [CalloutInfo("Shoplifting In Progress", CalloutProbability.High)]
    public class Shoplifting : TextCallout
    {
        private static readonly string[] Thieves =
        {
            "a_m_y_stlat_01", "a_m_y_soucent_01", "a_m_y_hipster_01", "a_f_y_vinewood_01", "a_m_m_soucent_01"
        };

        private bool _engaged;
        private bool _run;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Shoplifting In Progress",
                  "Store staff reporting a shoplifter leaving on foot.",
                  "Male or female, carrying a bag, walked out past the till.",
                  StreetNear(player.Position, 120f, 240f),
                  35f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "Staff have him on camera. He is on foot - do not let him get out of the block."; }
        }

        protected override void Build()
        {
            Suspect = SpawnPed(Thieves[Rng.Next(Thieves.Length)], CalloutPosition, Rng.Next(360));
            MarkStoppable(Suspect);

            Say("He is walking away from the store at " + Where(CalloutPosition) + ", heading towards the alley.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect)) { Close("the suspect is in custody"); return false; }

            var distance = Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position);
            if (!_engaged && distance < 22f)
            {
                _engaged = true;

                if (Rng.Next(100) < 70)
                {
                    _run = true;
                    Say("He has clocked you and he is running on foot.");
                    FleeOnFoot(Suspect);
                    Hud.Help("Do not lose him - get hands on before he clears the block.");
                }
                else
                {
                    Say("He is stopping. He knows he has been made.");
                    HandsUp(Suspect);
                }
            }

            if (_run && !PursuitRunning() && distance > 90f && !Dealt(Suspect))
            {
                // He got away. LSPDFR's search area is better than anything invented here, but the
                // callout cannot stay open forever.
                Close("the suspect is out of sight");
                return false;
            }

            return true;
        }
    }
}
