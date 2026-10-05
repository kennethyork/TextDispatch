using System;
using LSPD_First_Response.Mod.API;
using Rage;

namespace TextCallouts.Callouts
{
    /// <summary>
    /// The base for the callouts that are not police work at all.
    ///
    /// LSPDFR has no notion of an EMS callout: its callout list is police calls, and the agency you go
    /// on duty as changes your uniform, your vehicle and what dispatch calls you - nothing else reads it.
    /// What it does give a callout is the means to ask: Functions.GetCurrentAgencyScriptName() returns
    /// the agency's ScriptName from agency.xml. So these offer themselves only while the player is
    /// working as an emergency-medical agency, and stay out of the way the rest of the time - which is
    /// what makes going on duty as LSFD mean something.
    ///
    /// They all have the same shape, because that is what the work is: somebody is unwell at a place,
    /// you get there, you spend time on them, and an ambulance takes over. What differs is what is
    /// wrong with the patient and what the scene looks like.
    /// </summary>
    public abstract class EmsCallout : TextCallout
    {
        /// <summary>
        /// Which agencies these belong to, matched against the agency's ScriptName from agency.xml.
        ///
        /// Matched loosely - the name only has to contain one of these - because a player who adds
        /// their own ambulance service to agency.xml should not have to come back here. The two that
        /// ship with LSPDFR are lsfd, its EMS branch, and lsfd_fire.
        /// </summary>
        private static readonly string[] EmsWords = { "lsfd", "ems", "medic", "paramedic", "ambulance", "fire" };

        private static bool _reported;

        /// <summary>How long the player has to spend on the patient, standing next to them.</summary>
        protected virtual int TreatmentSeconds { get { return 15; } }

        /// <summary>How close counts as working on them.</summary>
        protected virtual float TreatmentRange { get { return 2.5f; } }

        /// <summary>How long the scene runs after the patient is stable, before it closes itself.</summary>
        protected virtual int HandoverSeconds { get { return 25; } }

        /// <summary>The person in trouble. The base class calls this the suspect; here it is the patient.</summary>
        protected Ped Patient;

        private bool _onScene;
        private bool _treating;
        private bool _treated;
        private bool _handingOver;
        private int _handoverAt;
        private int _lastTick;
        private int _workedMs;
        private int _nextHelp;

        /// <summary>
        /// Is the player working as an emergency-medical agency? It answers with the agency as well, so
        /// that a no can be read in the log rather than guessed at.
        /// </summary>
        protected static bool OnDutyAsEms(out string agency)
        {
            agency = null;

            try { agency = Functions.GetCurrentAgencyScriptName(); }
            catch (Exception ex) { Log.Error("asking which agency the player is on duty as", ex); return false; }

            if (string.IsNullOrEmpty(agency)) return false;

            var name = agency.Trim().ToLowerInvariant();
            foreach (var word in EmsWords)
                if (name.IndexOf(word, StringComparison.Ordinal) >= 0) return true;

            return false;
        }

        /// <summary>
        /// Whether to offer this callout at all. The reason for declining is logged once per session:
        /// LSPDFR asks a pack for a callout many times a patrol, and logging every refusal would be a
        /// wall of the same sentence.
        /// </summary>
        protected static bool OfferedToThisAgency()
        {
            string agency;
            if (OnDutyAsEms(out agency)) return true;

            if (!_reported)
            {
                _reported = true;
                Log.Line("the medical callouts are for an emergency-medical agency, and this patrol is '" +
                         (string.IsNullOrEmpty(agency) ? "not on duty as one" : agency) +
                         "' - so they are being left to the police ones");
            }

            return false;
        }

        /// <summary>
        /// Put somebody on the ground, alive and not going anywhere: what every one of these scenes has
        /// in common. A native rather than RPH's managed call, because that one is a short stumble that
        /// gets up by itself.
        /// </summary>
        protected void Collapse(Ped patient)
        {
            if (patient == null || !patient.Exists()) return;

            try { patient.BlockPermanentEvents = true; } catch { }
            try { Rage.Native.NativeFunction.Natives.SetPedToRagdoll(patient, -1, -1, 0, false, false, false); }
            catch (Exception ex) { Log.Error("putting a patient on the ground", ex); }
        }

        /// <summary>Sitting up, awake: what a patient does when the treatment has worked.</summary>
        protected void SitUp(Ped patient)
        {
            if (patient == null || !patient.Exists()) return;

            try { patient.Tasks.Clear(); } catch { }
            try { patient.Tasks.StandStill(-1); } catch { }
        }

        /// <summary>Called once, when the player first gets within sight of the scene.</summary>
        protected virtual void Arrived() { }

        /// <summary>Called once, when the player has spent long enough on the patient.</summary>
        protected virtual void Treated() { }

        /// <summary>
        /// Who is sent when the scene is under control. LSPDFR's own backup system, so a scene that
        /// needs an ambulance gets one; the fire callout asks for an engine instead.
        /// </summary>
        protected virtual void SummonBackup()
        {
            RequestAmbulance(CalloutPosition);
        }

        /// <summary>The line said when the patient is stable and help is on the way.</summary>
        protected virtual string Stable { get { return "The patient is stable. Get the ambulance to us."; } }

        /// <summary>What the player is doing, in the dispatcher's voice, while they work.</summary>
        protected virtual string Working { get { return "Work the patient - the ambulance is rolling."; } }

        protected override bool Tick()
        {
            var now = Environment.TickCount;
            var elapsed = _lastTick == 0 ? 0 : now - _lastTick;
            _lastTick = now;

            // A patient who dies on scene ends it, and signs off differently.
            if (Patient != null && Patient.Exists() && !Patient.IsAlive)
            {
                Close("the patient did not make it");
                return false;
            }

            var player = Game.LocalPlayer.Character;

            if (!_onScene)
            {
                if (player.Position.DistanceTo(CalloutPosition) < 30f)
                {
                    _onScene = true;
                    Say("You are on scene.");
                    try { Arrived(); } catch (Exception ex) { Log.Error("arriving", ex); }
                }
                return true;
            }

            if (_treated)
            {
                if (_handingOver && now - _handoverAt > HandoverSeconds * 1000)
                {
                    Close("handed over to the ambulance crew");
                    return false;
                }
                return true;
            }

            // Working on them: on foot, close, and for long enough. In a vehicle does not count - the
            // whole point of these is getting out and doing something.
            var close = false;
            try
            {
                close = Patient != null && Patient.Exists() &&
                        player.Position.DistanceTo(Patient.Position) <= TreatmentRange &&
                        !player.IsInAnyVehicle(false);
            }
            catch { }

            if (close)
            {
                if (!_treating)
                {
                    _treating = true;
                    _workedMs = 0;
                    _nextHelp = 0;
                    Say("You are with the patient. Stay with them.");
                }

                _workedMs += elapsed;

                // The countdown goes in the help box rather than the chat box: it changes every second,
                // and a chat line that changes every second is a chat line nobody reads.
                if (now >= _nextHelp)
                {
                    _nextHelp = now + 1000;
                    var left = Math.Max(0, TreatmentSeconds - _workedMs / 1000);
                    try { Hud.Help(Working + "  " + left + "s"); } catch { }
                }

                if (_workedMs >= TreatmentSeconds * 1000)
                {
                    _treated = true;
                    _treating = false;

                    try { Treated(); } catch (Exception ex) { Log.Error("treating the patient", ex); }

                    Say(Stable);
                    try { SummonBackup(); } catch (Exception ex) { Log.Error("calling for help", ex); }
                    _handingOver = true;
                    _handoverAt = now;
                }
            }
            else if (_treating)
            {
                _treating = false;
                _workedMs = 0;
                Say("You have stepped away - the patient is still waiting.");
            }

            return true;
        }
    }
}
