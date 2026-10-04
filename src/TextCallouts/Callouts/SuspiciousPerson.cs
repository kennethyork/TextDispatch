using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A caller reports somebody matching a description. There may be nothing in it - about half of
    /// these are an ordinary person who is reasonably annoyed at being stopped - or the person may be
    /// wanted, in which case they run.
    ///
    /// The point of it is that the player cannot know which until they do the work.
    /// </summary>
    [CalloutInfo("Suspicious Person", CalloutProbability.Medium)]
    public class SuspiciousPerson : TextCallout
    {
        private static readonly string[] People =
        {
            "a_m_y_business_01", "a_m_y_hipster_01", "a_m_m_soucent_01", "a_m_y_stlat_01",
            "a_f_y_business_01", "a_f_y_vinewood_01"
        };

        private bool _engaged;
        private bool _wanted;
        private int _engagedAt;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Suspicious Person",
                  "Caller reporting a person acting suspiciously nearby.",
                  "Looking into parked cars, trying door handles.",
                  StreetNear(player.Position, 110f, 220f),
                  30f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get
            {
                return "Caller description: dark clothing, checking car door handles. It may be nothing - " +
                       "but run him before you decide.";
            }
        }

        protected override void Build()
        {
            Suspect = SpawnPed(People[Rng.Next(People.Length)], CalloutPosition, Rng.Next(360));
            MarkStoppable(Suspect);

            _wanted = Rng.Next(100) < 45;

            Say("He is walking slowly along the parked cars at " + Where(CalloutPosition) + ".");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect)) { Close("you have him in custody"); return false; }

            if (!_engaged && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 20f)
            {
                _engaged = true;
                _engagedAt = Environment.TickCount;

                if (_wanted)
                {
                    // The record comes back over the radio, and what he does about it is decided by
                    // that - not by the model.
                    Say("Dispatch came back on him - he is wanted for burglary. Take him.");
                    FleeOnFoot(Suspect);
                }
                else
                {
                    Say("He is talking, and he has a good reason to be where he is. Your call.");
                }
            }

            if (!_wanted && _engaged && Environment.TickCount - _engagedAt > 60000)
            {
                Close("nothing in it - he was who he said he was");
                return false;
            }

            return true;
        }
    }
}
