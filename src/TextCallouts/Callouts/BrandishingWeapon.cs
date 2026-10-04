using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// Somebody waving a weapon about in public, who has not used it yet.
    ///
    /// That "yet" is the whole callout. Arrive slowly and there is a chance to talk them down into
    /// custody; drive at them with lights on and close the distance quickly and they panic. The same
    /// person, two very different outcomes, decided by how the player plays it - rather than by a dice
    /// roll that ignores what they did.
    /// </summary>
    [CalloutInfo("Weapons Call, Brandishing", CalloutProbability.Medium)]
    public class BrandishingWeapon : TextCallout
    {
        private static readonly string[] People =
        {
            "a_m_y_business_01", "a_m_m_business_01", "a_m_y_vinewood_01", "a_m_y_musclbeac_01"
        };

        private bool _engaged;
        private bool _resolved;
        private int _engagedAt;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Weapons Call, Brandishing",
                  "Male in the street with a handgun, threatening people with it.",
                  "He has not fired it. Several members of the public nearby.",
                  StreetNear(player.Position, 130f, 260f),
                  35f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get
            {
                return "He is armed but he has not used it. Go in hard and he may panic - slow it down and " +
                       "you might talk him into the cuffs.";
            }
        }

        protected override void Build()
        {
            Suspect = SpawnPed(People[Rng.Next(People.Length)], CalloutPosition, Rng.Next(360));
            MakeHostile(Suspect, "WEAPON_PISTOL", 60);
            MarkStoppable(Suspect);

            Say("He is at " + Where(CalloutPosition) + " with the weapon in his hand. Nobody has been hit.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect)) { Close("the suspect is in custody"); return false; }

            if (!_engaged && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 26f)
            {
                _engaged = true;
                _engagedAt = Environment.TickCount;

                // Where the player is when they first appear decides which way this goes - a warning
                // at a distance, not a verdict.
                if (Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 14f)
                {
                    _resolved = true;
                    Say("You are on him and he has panicked. He is raising it.");
                    MakeHostile(Suspect, "WEAPON_PISTOL", 60);
                    Hud.Help("Too close, too fast - cover.");
                }
                else
                {
                    Say("He has seen you and he has not run. Keep your distance and keep talking.");
                    Hud.Help("Stay back. Give him a reason to put it down.");
                }
            }

            if (_engaged && !_resolved)
            {
                var held = Environment.TickCount - _engagedAt;
                var stillKeepingDistance = Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) > 18f;

                // Standoff held for a few seconds without crowding him: he gives it up.
                if (held > 12000 && stillKeepingDistance)
                {
                    _resolved = true;
                    Say("He is putting it down. Take him - hands where you can see them.");
                    Surrender(Suspect);
                }
                else if (!stillKeepingDistance)
                {
                    _resolved = true;
                    Say("You have closed on him and he has decided to fight. He is raising it.");
                    MakeHostile(Suspect, "WEAPON_PISTOL", 60);
                }
            }

            return true;
        }
    }
}
