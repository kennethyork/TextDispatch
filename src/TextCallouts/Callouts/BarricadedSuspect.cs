using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// Somebody is held up inside with a weapon and is not coming out.
    ///
    /// The interesting part is that it has two endings rather than one. Rush it and it becomes a
    /// shooting; give it time and the pressure does the work, and they come out with their hands up -
    /// which is the ending the callout is really about, and the one a player only gets if they hold
    /// back.
    /// </summary>
    [CalloutInfo("Barricaded Suspect", CalloutProbability.Medium)]
    public class BarricadedSuspect : TextCallout
    {
        private static readonly string[] Suspects =
        {
            "a_m_y_business_01", "a_m_m_soucent_01", "a_m_y_stlat_01", "a_m_m_business_01"
        };

        private bool _engaged;
        private bool _decided;
        private bool _surrenders;
        private int _approachedAt;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Barricaded Suspect",
                  "Suspect has barricaded himself and is refusing to come out.",
                  "Armed with a handgun. Caller says he is alone inside.",
                  StreetNear(player.Position, 160f, 320f),
                  40f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get
            {
                return "He is armed and he is not coming out. Push him and this becomes a shooting - " +
                       "give him a minute to think about it first.";
            }
        }

        protected override int TimeoutMs { get { return 25 * 60 * 1000; } }

        protected override void Build()
        {
            Suspect = SpawnPed(Suspects[Rng.Next(Suspects.Length)], CalloutPosition, Rng.Next(360));
            MakeHostile(Suspect, "WEAPON_PISTOL", 120);
            MarkStoppable(Suspect);

            // He stays where he is until somebody makes him do otherwise: a barricaded suspect is
            // defined by not moving.
            try { Suspect.Tasks.StandStill(-1); } catch (Exception ex) { Log.Error("StandStill", ex); }

            // Roughly half are bluffing. Which half is fixed here, at the start - not decided later
            // to suit whatever the player did.
            _surrenders = Rng.Next(100) < 45;

            Say("He is inside at " + Where(CalloutPosition) + " and he is armed. Do not go in alone.");
            Hud.Help("Give him time - he may come out on his own. Call for backup either way.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect)) { Close("the suspect is in custody"); return false; }

            if (!_engaged && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 30f)
            {
                _engaged = true;
                _approachedAt = Environment.TickCount;
                Say("You are on top of him. He is watching you.");
            }

            if (_engaged && !_decided)
            {
                var held = Environment.TickCount - _approachedAt;

                // Getting close and staying close is what decides it. Standing off for a moment gives
                // him the chance to give up.
                var pressed = Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 12f;

                if (pressed || held > 45000)
                {
                    _decided = true;

                    if (_surrenders && !pressed)
                    {
                        Say("He is coming out. Hands where you can see them - take him.");
                        Surrender(Suspect);
                    }
                    else
                    {
                        Say("He has made his choice. He is coming out shooting.");
                        MakeHostile(Suspect, "WEAPON_PISTOL", 120);
                    }
                }
            }

            return true;
        }
    }
}
