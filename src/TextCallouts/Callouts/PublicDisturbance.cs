using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// A fight in the street, with more than two people in it.
    ///
    /// The two who are fighting are made to fight *each other* rather than the player, which is what
    /// makes it read as a brawl instead of a pair of hostiles. They stop fighting and scatter when
    /// police arrive - or they turn on you, depending on how many of them there are.
    /// </summary>
    [CalloutInfo("Public Disturbance", CalloutProbability.High)]
    public class PublicDisturbance : TextCallout
    {
        private static readonly string[] People =
        {
            "a_m_y_stlat_01", "a_m_m_soucent_01", "a_m_y_musclbeac_01", "a_m_y_hipster_01",
            "a_m_y_business_01", "a_m_y_vinewood_01"
        };

        private Ped _second;
        private Ped _third;
        private bool _engaged;

        public override bool OnBeforeCalloutDisplayed()
        {
            var player = Game.LocalPlayer.Character;
            Offer("Public Disturbance",
                  "Fight in progress, several people involved.",
                  "Caller reports two men fighting and others watching.",
                  StreetNear(player.Position, 120f, 250f),
                  35f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "They are going at each other. Separate them - this one starts as a fight, not a shooting."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            Suspect = SpawnPed(People[Rng.Next(People.Length)], position, Rng.Next(360));
            _second = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(1.4f, 0.6f, 0f), Rng.Next(360));
            _third = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(-1.8f, 1.2f, 0f), Rng.Next(360));

            // Two of them are fighting. The third is watching, and is not a suspect at all.
            Brawl(Suspect, _second);

            MarkStoppable(Suspect);
            MarkStoppable(_second);

            Say("Two of them are at it at " + Where(position) + ". A third is watching.");
        }

        protected override bool Tick()
        {
            if (Dealt(Suspect) && Dealt(_second)) { Close("both parties are in custody"); return false; }

            if (!_engaged && Game.LocalPlayer.Character.Position.DistanceTo(Suspect.Position) < 20f)
            {
                _engaged = true;

                // A crowd of two: usually they break and run rather than take on a police officer.
                if (Rng.Next(100) < 65)
                {
                    Say("They have seen you - both of them are running.");
                    FleeOnFoot(Suspect);
                    if (_second != null && _second.Exists()) FleeOnFoot(_second);
                    try { if (_third != null && _third.Exists()) _third.Tasks.Cower(-1); } catch { }
                    Hud.Help("Two runners. You cannot chase both - take the closer one.");
                }
                else
                {
                    Say("They are not backing down. They are turning on you.");
                    MakeHostile(Suspect, null, 0);
                    if (_second != null) MakeHostile(_second, null, 0);
                    try { if (_third != null && _third.Exists()) _third.Tasks.Flee(Game.LocalPlayer.Character, 120f, -1); } catch { }
                }
            }

            return true;
        }

        private static void Brawl(Ped first, Ped second)
        {
            if (first == null || second == null) return;

            try
            {
                first.CanAttackFriendlies = true;
                second.CanAttackFriendlies = true;
                first.Tasks.FightAgainst(second);
                second.Tasks.FightAgainst(first);
            }
            catch (Exception ex) { Log.Error("starting a brawl", ex); }
        }
    }
}
