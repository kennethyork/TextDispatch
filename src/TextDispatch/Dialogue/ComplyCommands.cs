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
        StandDown,
        Kneel,          // "get on your knees"
        TurnAround,     // "turn around and face the car"
        EngineOff,      // "turn the engine off"
        WindowDown,     // "roll the window down"
        FollowMe,       // "come with me"
        GetInMyCar      // "get in the back of the car"
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

            // StopThePed's orders, in words.
            if (Has(t, "on your knees", "get on your knees", "kneel", "get down on the ground", "down on the ground", "on the ground now"))
                return ComplyAction.Kneel;
            if (Has(t, "turn around", "face the car", "face away", "turn and face", "face the wall"))
                return ComplyAction.TurnAround;
            if (Has(t, "turn off the engine", "turn the engine off", "engine off", "turn the car off", "turn off the car", "kill the engine",
                        "shut off the engine", "shut the engine off", "switch off the engine", "switch the engine off"))
                return ComplyAction.EngineOff;
            if (Has(t, "roll down", "window down", "lower the window", "open the window", "open your window"))
                return ComplyAction.WindowDown;
            if (Has(t, "follow me", "come with me", "walk with me", "come over here", "come here"))
                return ComplyAction.FollowMe;
            if (Has(t, "get in the back", "in the back of my car", "in the back of the car", "get in my car", "get in the car", "sit in the back", "get in the cruiser"))
                return ComplyAction.GetInMyCar;

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
                case ComplyAction.Kneel: return "kneel";
                case ComplyAction.TurnAround: return "turn around";
                case ComplyAction.EngineOff: return "turn the engine off";
                case ComplyAction.WindowDown: return "roll the window down";
                case ComplyAction.FollowMe: return "follow me";
                case ComplyAction.GetInMyCar: return "get in the car";
                default: return "nothing";
            }
        }
    }
}
