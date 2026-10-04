using System;

namespace TextDispatch.Dialogue
{
    /// <summary>
    /// Something a pedestrian can be told to do, and actually does.
    ///
    /// This is what makes typed sentences act on the world instead of only producing a reply. It is
    /// deliberately plain pattern matching in our own code: "hands up" has to put hands up, and that
    /// is not something a language model is allowed to decide.
    /// </summary>
    public enum ComplyAction
    {
        None,
        HandsUp,
        LeaveVehicle,
        LookAtMe,
        HoldStill,
        StandDown
    }

    /// <summary>
    /// Recognising the handful of orders that carry an action. Kept free of any game reference so it
    /// can be tested outside the game, like the dispatch and dialogue classifiers.
    /// </summary>
    public static class ComplyCommands
    {
        public static ComplyAction Classify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return ComplyAction.None;

            var t = " " + text.ToLowerInvariant() + " ";

            // "you can go" is checked first: it contains "you" and would otherwise be swallowed by
            // nothing, but on a stop it is the sentence most likely to be said in passing.
            if (Has(t, "you can go", "you're free", "youre free", "free to go", "move along",
                        "on your way", "get out of here", "clear out", "walk away", "beat it"))
                return ComplyAction.StandDown;

            if (Has(t, "hands up", "hands where i can see", "let me see your hands", "show me your hands",
                        "raise your hands", "put your hands up", "hands on your head", "hands on the car"))
                return ComplyAction.HandsUp;

            if (Has(t, "step out", "get out of the car", "get out of the vehicle", "out of the vehicle",
                        "out of the car", "exit the vehicle", "get out and", "step out of the vehicle"))
                return ComplyAction.LeaveVehicle;

            if (Has(t, "look at me", "face me", "look this way", "eyes on me"))
                return ComplyAction.LookAtMe;

            if (Has(t, "don't move", "dont move", "do not move", "stay there", "stay put", "hold still",
                        "freeze", "wait here", "stay where you are", "nobody move"))
                return ComplyAction.HoldStill;

            return ComplyAction.None;
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>How the action reads in the log and in a refusal.</summary>
        public static string Describe(ComplyAction action)
        {
            switch (action)
            {
                case ComplyAction.HandsUp: return "hands up";
                case ComplyAction.LeaveVehicle: return "get out of the vehicle";
                case ComplyAction.LookAtMe: return "look at me";
                case ComplyAction.HoldStill: return "hold still";
                case ComplyAction.StandDown: return "stand down";
                default: return "nothing";
            }
        }
    }
}
