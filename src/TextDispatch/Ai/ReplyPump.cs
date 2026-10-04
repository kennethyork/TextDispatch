using System;
using System.Collections.Generic;

namespace TextDispatch.Ai
{
    /// <summary>
    /// Runs one model turn without ever blocking the game, and delivers the result as text.
    ///
    /// Two things make this necessary. A local model takes a second or two, which on the game fiber
    /// would be a visible freeze; and a model that is slow, unloaded or simply wrong must never
    /// leave somebody standing there mute. So every turn has a scripted line ready as a fallback,
    /// a deadline, and a delivery delay that makes the answer look typed rather than computed.
    ///
    /// Each conversation gets its own pump - a slow turn on the radio must not stall the suspect
    /// standing in front of you.
    /// </summary>
    internal sealed class ReplyPump
    {
        private readonly Settings _settings;
        private readonly List<Pending> _pending = new List<Pending>();
        private InFlight _inFlight;

        public ReplyPump(Settings settings)
        {
            _settings = settings;
        }

        /// <summary>True while a model turn is in flight.</summary>
        public bool Busy { get { return _inFlight != null; } }

        private sealed class Pending
        {
            public string Text;
            public int DueAt;
            public Action<string> Deliver;
        }

        private sealed class InFlight
        {
            public System.Threading.Tasks.Task<string> Work;
            public int StartedAt;
            public string Fallback;
            public Action<string> Deliver;
        }

        /// <summary>Deliver a line that is already known - a scripted reply - after a typing pause.</summary>
        public void Schedule(string text, Action<string> deliver)
        {
            Schedule(text, _settings.TypingMs, deliver);
        }

        /// <summary>
        /// As above, with the pause scaled from a given base - radio answers come back faster than
        /// somebody standing in front of you works out what to say.
        /// </summary>
        public void Schedule(string text, int baseMs, Action<string> deliver)
        {
            if (string.IsNullOrEmpty(text) || deliver == null) return;
            if (baseMs <= 0) baseMs = _settings.TypingMs;

            // Roughly what a person takes to type it, inside sane bounds.
            var delay = baseMs * (0.5 + text.Length / 60.0);
            delay = Math.Max(baseMs * 0.4, Math.Min(delay, baseMs * 2.2));

            _pending.Add(new Pending
            {
                Text = text,
                DueAt = Environment.TickCount + (int)delay,
                Deliver = deliver
            });
        }

        /// <summary>
        /// Start a model turn. If one is already running, the fallback is delivered instead - a
        /// conversation that overtakes itself reads worse than one that is briefly behind.
        /// </summary>
        public void Ask(Func<string> work, string fallback, Action<string> deliver)
        {
            if (_inFlight != null)
            {
                Schedule(fallback, deliver);
                return;
            }

            _inFlight = new InFlight
            {
                Work = System.Threading.Tasks.Task.Run(work),
                StartedAt = Environment.TickCount,
                Fallback = fallback,
                Deliver = deliver
            };
        }

        public void Update()
        {
            try
            {
                Collect();
                DeliverDue();
            }
            catch (Exception ex) { Log.Error("reply pump", ex); }
        }

        private void Collect()
        {
            if (_inFlight == null) return;

            if (_inFlight.Work != null && _inFlight.Work.IsCompleted)
            {
                string text = null;
                try { text = _inFlight.Work.Result; }
                catch (Exception ex) { Log.Error("model turn", ex); }

                var deliver = _inFlight.Deliver;
                var fallback = _inFlight.Fallback;
                _inFlight = null;

                Schedule(string.IsNullOrEmpty(text) ? fallback : text, deliver);
                return;
            }

            if (Environment.TickCount - _inFlight.StartedAt > _settings.AiTimeoutMs)
            {
                var deliver = _inFlight.Deliver;
                var fallback = _inFlight.Fallback;
                _inFlight = null;

                Log.Line("model did not answer in time; used the scripted line");
                Schedule(fallback, deliver);
            }
        }

        private void DeliverDue()
        {
            if (_pending.Count == 0) return;

            var now = Environment.TickCount;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var item = _pending[i];
                if (now - item.DueAt < 0) continue;

                _pending.RemoveAt(i);
                try { item.Deliver(item.Text); }
                catch (Exception ex) { Log.Error("deliver reply", ex); }
            }
        }
    }
}
