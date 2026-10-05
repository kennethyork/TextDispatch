using System;
using System.Collections.Generic;
using Rage;

namespace TextCallouts.Scripts
{
    /// <summary>
    /// The lines a callout's people will say when they are spoken to.
    ///
    /// A recipe builds a scene: people, vehicles, what they do when you arrive. What it could not do is
    /// give them anything to say, so a suspect who fled, gave up or cowered was mute - and the one thing
    /// a text interface is good at is talking. This is the missing half: a cookout registers lines
    /// against the people it spawned, and whoever is doing the talking (TextDispatch, if it is
    /// installed) asks here before it asks a language model.
    ///
    /// It is deliberately a static registry rather than an API one plugin calls into another: TextDispatch
    /// finds this type by reflection, in the same AppDomain, the same way it finds everything else about
    /// this pack. Nothing is referenced, so either plugin works without the other - without TextDispatch,
    /// these lines are simply never asked for.
    /// </summary>
    public static class CalloutScript
    {
        private sealed class Speaker
        {
            public string Name;
            public string[] Lines;
            public int At;
        }

        private static readonly Dictionary<PoolHandle, Speaker> Speakers = new Dictionary<PoolHandle, Speaker>();

        /// <summary>Give one of the scene's people something to say.</summary>
        public static void Register(Ped ped, string name, string[] lines)
        {
            if (ped == null || lines == null || lines.Length == 0) return;

            try
            {
                lock (Speakers)
                {
                    Speakers[ped.Handle] = new Speaker
                    {
                        Name = string.IsNullOrEmpty(name) ? "Someone at the scene" : name,
                        Lines = lines,
                        At = 0
                    };
                }
            }
            catch (Exception ex) { Log.Error("registering a script", ex); }
        }

        /// <summary>
        /// What this person says next, or null if they have nothing scripted.
        ///
        /// The lines are handed out in order and then the last one repeats: a person who has said their
        /// piece three times is still better company than one who has gone mute.
        /// </summary>
        public static string Reply(Ped ped, string said)
        {
            if (ped == null) return null;

            try
            {
                lock (Speakers)
                {
                    Speaker speaker;
                    if (!Speakers.TryGetValue(ped.Handle, out speaker)) return null;

                    var line = speaker.Lines[Math.Min(speaker.At, speaker.Lines.Length - 1)];
                    if (speaker.At < speaker.Lines.Length - 1) speaker.At++;
                    return line;
                }
            }
            catch (Exception ex) { Log.Error("reading a script", ex); return null; }
        }

        /// <summary>Whether this person has anything scripted - asked before the model is.</summary>
        public static bool Has(Ped ped)
        {
            if (ped == null) return false;

            try
            {
                lock (Speakers) { return Speakers.ContainsKey(ped.Handle); }
            }
            catch { return false; }
        }

        /// <summary>The name to put in front of their line, or null if they are not scripted.</summary>
        public static string NameOf(Ped ped)
        {
            if (ped == null) return null;

            try
            {
                lock (Speakers)
                {
                    Speaker speaker;
                    return Speakers.TryGetValue(ped.Handle, out speaker) ? speaker.Name : null;
                }
            }
            catch { return null; }
        }

        /// <summary>Forget one person. Called when a callout's scene is torn down.</summary>
        public static void Forget(Ped ped)
        {
            if (ped == null) return;

            try
            {
                lock (Speakers) { Speakers.Remove(ped.Handle); }
            }
            catch { }
        }

        /// <summary>
        /// Forget everybody. Called when a callout's scene is built, because a new scene's people are new
        /// people: a handle that has been reused by the game must never inherit the last script.
        /// </summary>
        public static void Clear()
        {
            try
            {
                lock (Speakers) { Speakers.Clear(); }
            }
            catch { }
        }

        /// <summary>How many people are currently scripted, for the log and for saying so in the box.</summary>
        public static int Count
        {
            get { try { lock (Speakers) { return Speakers.Count; } } catch { return 0; } }
        }
    }
}
