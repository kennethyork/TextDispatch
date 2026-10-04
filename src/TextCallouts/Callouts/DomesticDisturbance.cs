using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// Two people shouting at each other behind a front door. One of them turns on you.
    ///
    /// This is the callout that tests the "words and actions agree" rule the hard way: the line the
    /// pack says has to match what the ped actually does, so the hostility is decided first and the
    /// speech follows it.
    /// </summary>
    [CalloutInfo("Domestic Disturbance", CalloutProbability.High)]
    public class DomesticDisturbance : TextCallout
    {
        private static readonly string[] People =
        {
            "a_m_y_business_01", "a_f_y_business_01", "a_m_y_hipster_01", "a_f_y_hipster_01",
            "a_m_m_business_01", "a_f_m_business_02"
        };

        private Ped _other;
        private bool _engaged;
        private bool _hostile;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Domestic Disturbance",
                  "Neighbours reporting a disturbance at a residence.",
                  "A man and a woman shouting, sounds of a struggle.",
                  StreetNear(player.Position, 120f, 260f),
                  35f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get
            {
                return "Neighbours heard a struggle. Two people arguing - separate them and find out what " +
                       "happened before anybody leaves.";
            }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            Suspect = SpawnPed(People[Rng.Next(People.Length)], position, Rng.Next(360));
            _other = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(1.2f, 0.4f, 0f), Rng.Next(360));

            // Half of these are just an argument. The other half are not.
            _hostile = Rng.Next(100) < 55;

            _other.Tasks.StandStill(-1);
            MarkStoppable(Suspect);
            MarkStoppable(_other);

            Say("Both parties are out the front at " + Where(position) + ". Watch your hands.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect)) { Close("the aggressor is in custody"); return false; }

            if (!_engaged && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 18f)
            {
                _engaged = true;

                if (_hostile)
                {
                    Say("He is squaring up to you - he is not going to come quietly.");
                    MakeHostile(Suspect, null);          // angry, but not armed
                    Suspect.Tasks.FightAgainst(Game.LocalPlayer.Character);
                    _other.Tasks.Cower(-1);              // the other one has had enough
                }
                else
                {
                    Say("He is calming down. Talk to him before that changes.");
                    HandsUp(Suspect);
                    _other.Tasks.Cower(-1);
                }
            }

            if (_other != null && !_other.Exists() && !Dealt(Suspect)) { Close("one party left the scene"); return false; }
            return true;
        }
    }
}
