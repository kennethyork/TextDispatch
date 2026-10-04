using System;
using System.Collections.Generic;
using Rage;
using TextDispatch.Ai;
using TextDispatch.Chat;
using TextDispatch.Lspdfr;
using TextDispatch.Records;

namespace TextDispatch.Dialogue
{
    /// <summary>
    /// Conversation with the people around you.
    ///
    /// You talk by typing, exactly as on a roleplay server: plain text is speech, /w is a whisper,
    /// /shout carries further. Whoever is close enough answers in text - and, because LSPDFR knows
    /// whether they have been stopped, searched, cuffed or caught with something, they answer in a
    /// way that matches what is actually happening rather than at random.
    ///
    /// Replies are delayed by a short, human-looking pause. That pause is the difference between a
    /// chat and a lookup table.
    /// </summary>
    internal sealed class DialogueService
    {
        private readonly ChatBox _chat;
        private readonly LspdfrApi _api;
        private readonly Settings _settings;
        private readonly ReplyPump _pump;
        private readonly RecordsLedger _records;

        private readonly Dictionary<int, Talker> _talkers = new Dictionary<int, Talker>();
        private Talker _target;

        public DialogueService(ChatBox chat, LspdfrApi api, Settings settings, RecordsLedger records)
        {
            _chat = chat;
            _api = api;
            _settings = settings;
            _pump = new ReplyPump(settings);
            _records = records;
        }

        public Talker Target { get { return _target; } }

        /// <summary>Whether a model turn is mid-flight - used by tdstatus.</summary>
        public bool ModelBusy { get { return _pump.Busy; } }

        public void Update()
        {
            _pump.Update();
        }

        // ------------------------------------------------------------------ speech

        public void Say(string text, SpeechMode mode)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;

            switch (mode)
            {
                case SpeechMode.Whisper: _chat.Local("You whisper:", text); break;
                case SpeechMode.Shout: _chat.Local("You shout:", text); break;
                default: _chat.Local("You say:", text); break;
            }

            var partner = ResolveTarget(Range(mode));
            if (partner == null)
            {
                _chat.Do("There is nobody close enough to hear you.");
                return;
            }

            if (mode == SpeechMode.Shout) _chat.Do("Your voice carries down the street.");

            var state = BuildState(partner);

            // If that was an order, carry it out. Classification is ours, never the model's - and it
            // happens before the reply is generated so that what they say matches what they did.
            var order = ComplyCommands.Classify(text);
            if (order != ComplyAction.None) PerformOrder(partner, state, order);

            partner.Memory.Add("You: " + text);

            var intent = ScriptedReplies.Classify(text);
            var scripted = ScriptedReplies.Answer(text, intent, partner, state);
            var speaker = partner;

            if (!_settings.UseModel)
            {
                Respond(speaker, scripted);
                return;
            }

            _pump.Ask(
                () =>
                {
                    string error;
                    var reply = NpcPrompt.Ask(_settings, speaker, state, CharacterFor(speaker), text, out error);
                    if (reply == null && error != null) Log.Line("npc model: " + error);
                    return reply;
                },
                scripted,
                line => Respond(speaker, line));
        }

        /// <summary>
        /// Carry out an order the player typed, if this person is willing.
        ///
        /// Willingness follows temperament, so the action and the reply agree. The scripted answers for
        /// a compliant pedestrian already read as compliance and a hostile one's already read as
        /// refusal - making everyone obey regardless would put "Make me." next to somebody raising
        /// their hands.
        /// </summary>
        private void PerformOrder(Talker talker, PedState state, ComplyAction order)
        {
            bool willing =
                state.Arrested ||
                talker.Mood == Temperament.Compliant ||
                talker.Mood == Temperament.Nervous ||
                (talker.Mood == Temperament.Defensive &&
                 new Random(talker.Handle * 31 + (int)order * 7).Next(100) < 70);

            if (!willing)
            {
                Log.Line("order: " + talker.Name + " refused '" + ComplyCommands.Describe(order) +
                         "' (mood " + talker.Mood + ")");
                return;
            }

            string detail;
            bool done = PedActions.Perform(_api, talker.Ped, order, out detail);

            Log.Line("order: " + talker.Name + " '" + ComplyCommands.Describe(order) + "' -> " +
                     (done ? "done (" + detail + ")" : "FAILED (" + detail + ")"));
        }

        /// <summary>
        /// Who this person is, for the prompt. Read from the records ledger, so the conversation and the
        /// terminal describe the same person from the same source.
        /// </summary>
        private string CharacterFor(Talker talker)
        {
            if (talker == null || _records == null) return null;

            var person = _records.EnsurePerson(talker.Handle, talker.Name);
            return person == null ? null : person.CharacterLine();
        }

        /// <summary>
        /// Record something the player did, without making anybody answer it.
        ///
        /// /me and /do are part of what is happening in the scene. Leaving them out of the transcript
        /// meant somebody could announce that they had just drawn a baton and the conversation would
        /// carry on as though nothing had happened.
        /// </summary>
        public void Note(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            var talker = _target;
            if (talker == null)
            {
                var nearest = _api.NearestPed(_settings.SayRange);
                if (nearest != null) talker = For(nearest);
            }

            if (talker != null) talker.Memory.Add(line);
        }

        /// <summary>Put a line in the transcript, as the person who said it.</summary>
        private void Respond(Talker talker, string line)
        {
            if (talker == null || string.IsNullOrEmpty(line)) return;

            _chat.Local(talker.Name + ":", line);
            talker.Remember(talker.Name + ": " + line);
        }

        private float Range(SpeechMode mode)
        {
            switch (mode)
            {
                case SpeechMode.Whisper: return _settings.WhisperRange;
                case SpeechMode.Shout: return _settings.ShoutRange;
                default: return _settings.SayRange;
            }
        }

        /// <summary>
        /// Who you are talking to. An explicit target wins while they are still in range, so a
        /// conversation does not silently jump to a passer-by mid-sentence.
        /// </summary>
        private Talker ResolveTarget(float range)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) return null;

            if (_target != null && _target.Valid() && _target.DistanceTo(player) <= range) return _target;
            _target = null;

            var nearest = _api.NearestPed(range);
            return nearest == null ? null : For(nearest);
        }

        private Talker For(Ped ped)
        {
            var handle = unchecked((int)ped.Handle.Value);

            Talker talker;
            if (_talkers.TryGetValue(handle, out talker))
            {
                if (talker.Valid()) return talker;
            }

            talker = new Talker
            {
                Ped = ped,
                Handle = handle,
                Name = Identities.NameFor(handle),
                Mood = Identities.MoodFor(handle)
            };
            _talkers[handle] = talker;
            return talker;
        }

        private PedState BuildState(Talker talker)
        {
            var state = new PedState();
            var ped = talker.Ped;

            try
            {
                state.Arrested = _api.PedArrested(ped);
                state.Stopped = _api.PedStoppedByPlayer(ped);
                state.Frisked = _api.PedFrisked(ped);
                state.Contraband = _api.PedCarryingContraband(ped);
                state.Surrendered = _api.PedSurrendered(ped);
                state.Identified = _api.PedIdentified(ped);
                state.Persona = _api.PersonaForPed(ped);
                state.InPursuit = _api.ActivePursuit() != null;

                var callout = _api.CurrentCallout();
                if (callout != null)
                {
                    state.CalloutRunning = true;
                    state.CalloutName = _api.CalloutFriendlyName(callout) ?? _api.CalloutName(callout);
                }

                state.PlayerOnDuty = _api.PedIsCop(Game.LocalPlayer.Character) ||
                                     state.CalloutRunning ||
                                     _api.PlayerPerformingPullover();

                state.Zone = _api.ZoneAt(ped.Position);
            }
            catch (Exception ex) { Log.Error("dialogue state", ex); }

            talker.Mood = Identities.Adjust(talker.Mood, state);
            return state;
        }

        // ------------------------------------------------------------------ who is around

        private List<Talker> Nearby()
        {
            var player = Game.LocalPlayer.Character;
            var nearby = new List<Talker>();
            if (player == null) return nearby;

            try
            {
                foreach (var ped in World.GetAllPeds())
                {
                    if (ped == null || ReferenceEquals(ped, player)) continue;
                    if (!ped.Exists() || !ped.IsAlive) continue;

                    var distance = ped.Position.DistanceTo(player.Position);
                    if (distance > 60f) continue;
                    nearby.Add(For(ped));
                }
            }
            catch (Exception ex) { Log.Error("nearby people", ex); }

            nearby.Sort((a, b) => a.DistanceTo(player).CompareTo(b.DistanceTo(player)));
            return nearby;
        }

        public void WhoIsAround()
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) { _chat.Error("No player ped."); return; }

            var nearby = Nearby();
            if (nearby.Count == 0) { _chat.Do("There is nobody around."); return; }

            _chat.Notice("People nearby - '/talk <n>' to speak to one:");
            for (int i = 0; i < nearby.Count && i < 8; i++)
            {
                var talker = nearby[i];
                var mark = ReferenceEquals(talker, _target) ? "  <- talking to" : "";
                _chat.Notice("  " + (i + 1) + ". " + talker.Name + "  (" +
                             talker.DistanceTo(player).ToString("0.0") + "m, " +
                             talker.Mood.ToString().ToLowerInvariant() + ")" + mark);
            }
        }

        public void TalkTo(int index)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) { _chat.Error("No player ped."); return; }

            var nearby = Nearby();
            if (index < 1 || index > nearby.Count)
            {
                _chat.Error("There is no number " + index + " nearby. Try /who.");
                return;
            }

            _target = nearby[index - 1];
            _chat.Notice("You turn to " + _target.Name + " (" +
                         _target.Mood.ToString().ToLowerInvariant() + ").");
        }

        public void StopTalking()
        {
            if (_target == null) { _chat.Notice("You were not talking to anyone in particular."); return; }
            _chat.Notice("You stop talking to " + _target.Name + ".");
            _target = null;
        }
    }
}
