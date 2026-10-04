using System;
using System.Collections.Generic;
using Rage;

namespace TextDispatch.Dialogue
{
    /// <summary>How far the player's line carries. GTA World splits speech into ranges; so does this.</summary>
    public enum SpeechMode
    {
        Say,
        Whisper,
        Shout
    }

    /// <summary>
    /// Who somebody is when you talk to them. Fixed per person for the whole session - derived from
    /// the ped's handle, so the same suspect behaves the same way every time you meet them, which is
    /// the difference between a character and a dice roll.
    /// </summary>
    public enum Temperament
    {
        Compliant,
        Nervous,
        Defensive,
        Hostile
    }

    public enum DialogueIntent
    {
        Greeting,
        Identify,
        WhyStopped,
        WhatHappened,
        Weapons,
        Explain,
        Calm,
        Command,
        Arrest,
        Threat,
        Insult,
        Thanks,
        SmallTalk,
        Question,
        Unknown
    }

    /// <summary>
    /// What is actually true about this person and this moment. Gathered from LSPDFR and the game,
    /// and the only thing an NPC is allowed to know.
    /// </summary>
    public sealed class PedState
    {
        public bool Arrested;
        public bool Stopped;
        public bool Frisked;
        public bool Contraband;
        public bool Surrendered;
        public bool Identified;
        public bool InPursuit;
        public bool CalloutRunning;
        public bool PlayerOnDuty;

        public string CalloutName;
        public string Zone;
        public string Persona;

        public string Describe()
        {
            var parts = new List<string>();
            if (PlayerOnDuty) parts.Add("a police officer on duty");
            else parts.Add("a civilian");

            if (InPursuit) parts.Add("you are running from them");
            else if (Arrested) parts.Add("you are under arrest and in handcuffs");
            else if (Stopped) parts.Add("you have been stopped by them");
            else parts.Add("you are going about your day");

            if (Frisked) parts.Add(Contraband ? "you were searched and they found something illegal" : "you were searched and they found nothing");
            if (Surrendered) parts.Add("you have given up");
            if (!string.IsNullOrEmpty(Zone)) parts.Add("you are in " + Zone);
            if (CalloutRunning && !string.IsNullOrEmpty(CalloutName)) parts.Add("an incident is in progress: " + CalloutName);

            return string.Join(". ", parts.ToArray()) + ".";
        }

        public string Key()
        {
            return (Arrested ? "a" : "-") + (Stopped ? "s" : "-") + (InPursuit ? "p" : "-") +
                   (Frisked ? "f" : "-") + (Contraband ? "c" : "-") + (Surrendered ? "u" : "-");
        }
    }

    /// <summary>
    /// A person you can talk to: their handle, the name they are known by, their temperament, and
    /// what has been said between you.
    /// </summary>
    public sealed class Talker
    {
        public Ped Ped;
        public int Handle;
        public string Name;
        public Temperament Mood;
        public readonly List<string> Memory = new List<string>();

        public bool Valid()
        {
            try { return Ped != null && Ped.Exists(); }
            catch { return false; }
        }

        public float DistanceTo(Ped player)
        {
            try
            {
                if (player == null || Ped == null) return float.MaxValue;
                return Ped.Position.DistanceTo(player.Position);
            }
            catch { return float.MaxValue; }
        }

        public void Remember(string line)
        {
            Memory.Add(line);

            // Long enough for a real conversation. Ten lines was roughly two exchanges, which meant
            // a model was answering without knowing what it had just said.
            if (Memory.Count > 60) Memory.RemoveAt(0);
        }

        /// <summary>The last few lines, oldest first, for the model's transcript.</summary>
        public string Transcript(int lines)
        {
            if (lines <= 0) lines = 12;
            var start = Math.Max(0, Memory.Count - lines);
            return string.Join("\n", Memory.GetRange(start, Memory.Count - start).ToArray());
        }
    }

    /// <summary>Deterministic identity: the same ped always gets the same name and temperament.</summary>
    internal static class Identities
    {
        private static readonly string[] FirstNames =
        {
            "Marcus", "Dwayne", "Tyrone", "Luis", "Hector", "Andre", "Rico", "Anton",
            "Marisol", "Yolanda", "Denise", "Tanisha", "Rosa", "Elena", "Kiana", "Simone",
            "Derek", "Brett", "Chad", "Corey", "Nate", "Owen", "Ruben", "Vince",
            "Mandy", "Kirsty", "Trisha", "Nadia", "Priya", "Amara", "Lena", "Georgia"
        };

        private static readonly string[] LastNames =
        {
            "Reyes", "Vasquez", "Okafor", "Bennett", "Delgado", "Hollis", "Marchetti", "Novak",
            "Whitlock", "Barnes", "Cole", "Fitzgerald", "Rahman", "Silva", "Tran", "Kowalski",
            "Mackenzie", "Doyle", "Farrow", "Ellison", "Greaves", "Ibarra", "Lomax", "Pruitt"
        };

        public static string NameFor(int handle)
        {
            var rng = new Random(handle);
            return FirstNames[rng.Next(FirstNames.Length)] + " " + LastNames[rng.Next(LastNames.Length)];
        }

        public static Temperament MoodFor(int handle)
        {
            var rng = new Random(handle * 31 + 7);
            var roll = rng.Next(100);

            if (roll < 45) return Temperament.Compliant;
            if (roll < 70) return Temperament.Nervous;
            if (roll < 88) return Temperament.Defensive;
            return Temperament.Hostile;
        }

        /// <summary>Being arrested or caught with something changes how somebody behaves.</summary>
        public static Temperament Adjust(Temperament mood, PedState state)
        {
            if (state.Arrested)
                return mood == Temperament.Compliant ? Temperament.Defensive : mood;

            if (state.Contraband && mood == Temperament.Compliant)
                return Temperament.Nervous;

            if (state.InPursuit)
                return Temperament.Hostile;

            return mood;
        }
    }
}
