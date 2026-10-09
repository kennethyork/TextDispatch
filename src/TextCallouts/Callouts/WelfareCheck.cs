using System;
using LSPD_First_Response.Mod.Callouts;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// The call that is nine times out of ten nothing: somebody sat somewhere they might not be, and a
    /// neighbour who is worried. Half the time the person is fine and the call is over in a minute;
    /// the other half they are not, and it turns into the rest of the medical callouts.
    ///
    /// Offered only while the player is on duty as an emergency-medical agency (see EmsCallout).
    /// </summary>
    [CalloutInfo("Welfare Check", CalloutProbability.High)]
    public class WelfareCheck : EmsCallout
    {
        private static readonly string[] People =
        {
            "a_m_m_tramp_01", "a_m_o_tramp_01", "a_m_m_trampbeac_01", "a_m_y_vinewood_01", "a_f_m_tramp_01"
        };

        private static readonly string[] Fine =
        {
            "I am fine. I have been sat here every afternoon for eleven years.",
            "Do I look like I need a doctor? I am waiting for a bus.",
            "You people. Twice this month the neighbours have sent somebody."
        };

        private static readonly string[] Unwell =
        {
            "I - I do not feel right. My chest is tight.",
            "I have not eaten since Tuesday, I think.",
            "Everything is spinning. Has been since this morning."
        };

        private bool _needsHelp;
        private string _line;

        public override bool OnBeforeCalloutDisplayed()
        {
            if (!OfferedToThisAgency()) return false;

            var player = Game.LocalPlayer.Character;
            Offer("Welfare Check",
                  "Welfare check on a person sat in the open, reported by a neighbour.",
                  "No information on whether they need medical help.",
                  StreetNear(player.Position, 150f, 300f),
                  40f);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Briefing
        {
            get { return "Nobody knows yet whether this is anything. Ask them, and take what they say seriously either way."; }
        }

        protected override string Working { get { return "Check on them - ask if they are all right."; } }

        protected override int TreatmentSeconds { get { return 8; } }

        protected override string Stable
        {
            get { return "Report the findings back when you have finished with them."; }
        }

        protected override void Build()
        {
            var position = CalloutPosition;

            _needsHelp = Rng.Next(100) < 45;
            _line = _needsHelp ? Unwell[Rng.Next(Unwell.Length)] : Fine[Rng.Next(Fine.Length)];

            Patient = SpawnPed(People[Rng.Next(People.Length)], position, Rng.Next(360));
            if (_needsHelp) Hurt(Patient, 70);
            try { Patient.Tasks.StandStill(-1); } catch { }

            if (_needsHelp) Collapse(Patient);

            Say("Welfare check at " + Where(position) + ". A neighbour called it in, says they have not moved in hours.");
        }

        protected override void Arrived()
        {
            Say("The person looks up as you approach. \"About time. What do you want?\"");
        }

        protected override void Treated()
        {
            Say("Patient: " + _line);
        }

        /// <summary>Only half of these need an ambulance, and sending one to a man waiting for a bus is
        /// the kind of thing a dispatcher remembers.</summary>
        protected override void SummonBackup()
        {
            if (!_needsHelp)
            {
                Log.Line("welfare check: no medical need at " + Where(CalloutPosition));
                return;
            }

            base.SummonBackup();
        }
    }
}
