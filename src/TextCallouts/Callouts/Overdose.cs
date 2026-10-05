using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// Somebody has taken too much of something and is barely in the room. The treatment is shorter
    /// than a cardiac arrest and the patient comes round, which makes it the one medical callout where
    /// the scene ends better than it started.
    ///
    /// Offered only while the player is on duty as an emergency-medical agency (see EmsCallout).
    /// </summary>
    [CalloutInfo("Overdose", CalloutProbability.Medium)]
    public class Overdose : EmsCallout
    {
        private static readonly string[] People =
        {
            "a_m_y_hipster_01", "a_f_y_hipster_01", "a_m_y_beach_01", "a_f_y_beach_01", "a_m_y_skater_01"
        };

        private static readonly string[] Waking =
        {
            "What - what happened? Where am I?",
            "I only took one. I swear I only took one.",
            "Are you a doctor? Am I in trouble?"
        };

        private Ped _friend;

        public override bool OnBeforeCalloutDisplayed()
        {
            if (!OfferedToThisAgency()) return false;

            var player = Game.LocalPlayer.Character;
            Offer("Overdose",
                  "Person unresponsive, possibly overdosed.",
                  "A friend is with them. Naloxone is in your kit, not theirs.",
                  StreetNear(player.Position, 150f, 300f),
                  40f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "Airway first, then naloxone, then stay with them - they come round frightened and you are the only familiar thing there."; }
        }

        protected override string SignOff
        {
            get { return "They are awake and arguing, which is the best outcome this call has. 10-8."; }
        }

        protected override string Working { get { return "Airway, then naloxone. Stay with them."; } }

        protected override int TreatmentSeconds { get { return 12; } }

        protected override string Stable
        {
            get { return "They are breathing properly again and starting to come round. Ambulance is rolling."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            Patient = SpawnPed(People[Rng.Next(People.Length)], position, Rng.Next(360));
            Patient.Health = 70;
            Collapse(Patient);

            _friend = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(1.6f, 0.7f, 0f), Rng.Next(360));
            try { _friend.Tasks.StandStill(-1); } catch { }

            Say("Person unresponsive at " + Where(position) + ". Caller says they have been like that a few minutes.");
        }

        protected override void Arrived()
        {
            if (_friend == null || !_friend.Exists()) return;
            Say("Caller: it was just one, I told them it was too much - please, is she all right?");
        }

        protected override void Treated()
        {
            SitUp(Patient);
            Say("Patient: " + Waking[Rng.Next(Waking.Length)]);
        }
    }
}
