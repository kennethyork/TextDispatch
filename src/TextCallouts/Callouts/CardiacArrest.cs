using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// Somebody has collapsed in the street and a stranger is standing over them. Nobody to arrest, no
    /// vehicle to run, and twenty seconds of the player's time on their knees - which is the point: a
    /// callout pack for an emergency-medical patrol should contain work that is not a chase.
    ///
    /// Offered only while the player is on duty as an emergency-medical agency (see EmsCallout).
    /// </summary>
    [CalloutInfo("Cardiac Arrest", CalloutProbability.Medium)]
    public class CardiacArrest : EmsCallout
    {
        private static readonly string[] People =
        {
            "a_m_y_business_01", "a_m_m_business_01", "a_f_y_business_01", "a_m_y_vinewood_01",
            "a_f_y_vinewood_01", "a_m_m_farmer_01"
        };

        private Ped _bystander;
        private bool _bystanderSpoken;

        public override bool OnBeforeCalloutDisplayed()
        {
            if (!OfferedToThisAgency()) return false;

            var player = Game.LocalPlayer.Character;
            Offer("Cardiac Arrest",
                  "Man down, not breathing. Caller is with him.",
                  "CPR in progress by a member of the public. Ambulance is not on scene.",
                  StreetNear(player.Position, 150f, 300f),
                  40f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "Get on him and start compressions. The ambulance is coming, but you are what he has until then."; }
        }

        protected override string SignOff
        {
            get { return "Medics have him. You did the part that mattered - show me 10-8."; }
        }

        protected override string Working { get { return "Compressions - do not stop."; } }

        protected override int TreatmentSeconds { get { return 20; } }

        protected override string Stable
        {
            get { return "He is breathing again. Keep him where he is until the ambulance arrives."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            Patient = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(0.5f, 0.4f, 0f), Rng.Next(360));
            Patient.Health = 60;
            Collapse(Patient);

            // Somebody who called it in and is still standing over him, which is why the scene reads as
            // a real one rather than a body on a pavement.
            _bystander = SpawnPed(People[Rng.Next(People.Length)], position + new Vector3(1.8f, -0.6f, 0f), Rng.Next(360));
            try { _bystander.Tasks.StandStill(-1); } catch { }

            Say("Man down at " + Where(position) + ", not breathing. Caller is with him.");
        }

        protected override void Arrived()
        {
            if (_bystanderSpoken || _bystander == null || !_bystander.Exists()) return;
            _bystanderSpoken = true;

            Say("Caller: he just went down, I did not see him hit anything. I started pushing on his chest like they say to.");
            try { Hud.Help("Get on the ground with the patient and stay with them."); } catch { }
        }

        protected override void Treated()
        {
            // Nobody gets up from a cardiac arrest: he is breathing, and that is all that can be said.
            Say("Patient: (a weak breath)");
        }
    }
}
