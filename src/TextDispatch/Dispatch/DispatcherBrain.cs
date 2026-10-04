using System;
using System.Collections.Generic;
using System.Text;

namespace TextDispatch.Dispatch
{
    /// <summary>What the unit just did on the radio.</summary>
    internal enum DispatcherIntent
    {
        Status,
        Backup,
        Ems,
        Fire,
        Transport,
        Report,
        Question,
        Instruction,
        Unknown
    }

    /// <summary>
    /// What dispatch knows. Everything the dispatcher is allowed to refer to, and nothing else -
    /// which is what stops it announcing units that were never sent.
    /// </summary>
    internal sealed class DispatchContext
    {
        public string Unit;
        public string Zone;
        public bool PlayerOnDuty;
        public bool CalloutRunning;
        public string CalloutName;
        public string CalloutState;
        public bool OnScene;
        public bool Pursuit;
        public bool Pullover;
        public bool Available;
        public string LastStatus;

        /// <summary>What dispatch actually sent, if anything. Filled in only on a real request.</summary>
        public string UnitsDispatched;

        public string Describe()
        {
            var parts = new List<string>();
            parts.Add("You are the dispatcher. The unit transmitting is " + Unit + ".");

            if (!PlayerOnDuty) parts.Add("That unit is off duty.");
            else if (CalloutRunning)
            {
                var name = string.IsNullOrEmpty(CalloutName) ? "a call" : CalloutName;
                parts.Add("They are assigned to " + name + ".");
                if (!string.IsNullOrEmpty(CalloutState)) parts.Add("That call is " + CalloutState + ".");
                if (OnScene) parts.Add("They have advised they are on scene.");
            }
            else parts.Add("They have no call assigned.");

            if (Pursuit) parts.Add("There is a pursuit in progress.");
            else if (Pullover) parts.Add("They are on a traffic stop.");

            if (Available) parts.Add("They show as available.");
            if (!string.IsNullOrEmpty(Zone)) parts.Add("Their last known location is " + Zone + ".");
            if (!string.IsNullOrEmpty(LastStatus)) parts.Add("Their last status was " + LastStatus + ".");

            // Stated only when it really happened, so a dispatcher can mention it truthfully.
            if (!string.IsNullOrEmpty(UnitsDispatched)) parts.Add("You have already dispatched " + UnitsDispatched + " to them.");
            else parts.Add("You have not dispatched anyone to them yet.");

            return string.Join(" ", parts.ToArray());
        }
    }

    /// <summary>
    /// The dispatcher's half of the radio net.
    ///
    /// This is where the split between words and action lives. Working out *what the unit is asking
    /// for* is deterministic code, because asking for backup has to actually request backup. Deciding
    /// *how to answer* is the model's job when one is available, and a script when it is not.
    ///
    /// The dispatcher is held to one rule the model cannot be trusted with on its own: it may not
    /// say units are coming unless they really were requested. That is checked, not just asked for.
    /// </summary>
    internal static class DispatcherBrain
    {
        // ------------------------------------------------------------------ classification

        /// <summary>A bare 10-code anywhere in the line, or null.</summary>
        public static string DetectCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var t = " " + text.ToLowerInvariant().Replace(".", " ").Replace(",", " ") + " ";

            if (t.IndexOf(" 10-97 ", StringComparison.Ordinal) >= 0 || t.IndexOf(" ten ninety seven ", StringComparison.Ordinal) >= 0) return "10-97";
            if (t.IndexOf(" 10-98 ", StringComparison.Ordinal) >= 0 || t.IndexOf(" ten ninety eight ", StringComparison.Ordinal) >= 0) return "10-98";
            if (t.IndexOf(" 10-13 ", StringComparison.Ordinal) >= 0 || t.IndexOf(" ten thirteen ", StringComparison.Ordinal) >= 0) return "10-13";
            if (t.IndexOf(" 10-8 ", StringComparison.Ordinal) >= 0 || t.IndexOf(" ten eight ", StringComparison.Ordinal) >= 0) return "10-8";
            if (t.IndexOf(" 10-7 ", StringComparison.Ordinal) >= 0 || t.IndexOf(" ten seven ", StringComparison.Ordinal) >= 0) return "10-7";
            if (t.IndexOf(" 10-6 ", StringComparison.Ordinal) >= 0 || t.IndexOf(" ten six ", StringComparison.Ordinal) >= 0) return "10-6";

            if (t.IndexOf(" code 3 ", StringComparison.Ordinal) >= 0 || t.IndexOf(" code-3 ", StringComparison.Ordinal) >= 0) return "code-3";
            if (t.IndexOf(" code 4 ", StringComparison.Ordinal) >= 0 || t.IndexOf(" code-4 ", StringComparison.Ordinal) >= 0) return "code-4";

            return null;
        }

        public static DispatcherIntent Classify(string text)
        {
            var t = " " + (text ?? "").ToLowerInvariant() + " ";

            if (Has(t, "ambulance", "ems", "medic", "paramedic", "10-52", "injury", "injured", "bleeding",
                        "not breathing", "unconscious", "overdose", "chest pain", "needs a doctor"))
                return DispatcherIntent.Ems;

            if (Has(t, "fire truck", "firetruck", "fire department", "smoke", "on fire", "burning", "structure fire"))
                return DispatcherIntent.Fire;

            if (Has(t, "transport", "prisoner", "paddy wagon", "pick him up", "pick them up", "haul him", "haul them"))
                return DispatcherIntent.Transport;

            if (Has(t, "backup", "10-13", "assistance", "assist", "send a unit", "send units", "more units",
                        "additional units", "another unit", "cover me", "officer needs",
                        // A reported violent incident is a request for units, whether or not it is
                        // worded as one. This is what makes "/911 man with a gun" actually send one.
                        "gun", "weapon", "knife", "armed", "shots fired", "shooting", "shot", "stabbed",
                        "stabbing", "fight", "assault", "robbery", "burglary", "break in", "break-in",
                        "prowler", "trespass", "suspicious", "wanted", "hostage", "carjacking"))
                return DispatcherIntent.Backup;

            if (Has(t, "do i have", "any warrants", "run him", "run her", "check on", "what's the status",
                        "whats the status", "status on", "any update", "is there a", "can you"))
                return DispatcherIntent.Question;

            if (t.IndexOf('?') >= 0) return DispatcherIntent.Question;

            if (Has(t, "i have", "i've got", "ive got", "heading", "in pursuit of", "at the corner",
                        "male", "female", "suspect is", "vehicle is", "plate is", "showing", "advising"))
                return DispatcherIntent.Report;

            if (Has(t, "requesting", "need a", "i need", "send me", "start me", "clear me", "10-22"))
                return DispatcherIntent.Instruction;

            return DispatcherIntent.Unknown;
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>Whether an action is needed to make an answer true.</summary>
        public static bool NeedsAction(DispatcherIntent intent)
        {
            return intent == DispatcherIntent.Backup || intent == DispatcherIntent.Ems ||
                   intent == DispatcherIntent.Fire || intent == DispatcherIntent.Transport;
        }

        /// <summary>
        /// True when a reply claims responders are coming. Used to reject a model answer that
        /// promises units nobody requested - the one thing a dispatcher must never invent.
        /// </summary>
        public static bool ClaimsUnitsIncoming(string reply)
        {
            if (string.IsNullOrEmpty(reply)) return false;
            var t = reply.ToLowerInvariant();

            return Has(t, "en route", "enroute", "on the way", "responding", "eta ", "rolling your way",
                           "heading your way", "inbound");
        }

        // ------------------------------------------------------------------ scripted voice

        public static string Scripted(DispatcherIntent intent, DispatchContext ctx, string text)
        {
            var unit = ctx.Unit;
            var rng = new Random(Environment.TickCount);

            switch (intent)
            {
                case DispatcherIntent.Backup:
                    return Pick(rng,
                        "Copy " + unit + ", backup en route. Stand by.",
                        "10-4 " + unit + ", sending a unit your way.",
                        "Copy. Units responding to your location.");

                case DispatcherIntent.Ems:
                    return Pick(rng,
                        "Copy " + unit + ", EMS notified and rolling.",
                        "10-4 " + unit + ", ambulance is en route.",
                        "Copy " + unit + ", medical is on the way.");

                case DispatcherIntent.Fire:
                    return Pick(rng,
                        "Copy " + unit + ", fire department is responding.",
                        "10-4 " + unit + ", LSFD en route to your location.");

                case DispatcherIntent.Transport:
                    return Pick(rng,
                        "Copy " + unit + ", transport unit en route.",
                        "10-4 " + unit + ", prisoner transport on the way.");

                case DispatcherIntent.Report:
                    if (ctx.CalloutRunning && !string.IsNullOrEmpty(ctx.CalloutName))
                        return Pick(rng,
                            "Copy " + unit + ", noted on the " + ctx.CalloutName + " log.",
                            "10-4 " + unit + ", I have that.",
                            "Copy that, " + unit + ". Keep me advised.");
                    return Pick(rng,
                        "Copy " + unit + ", logged.",
                        "10-4 " + unit + ", I have that.");

                case DispatcherIntent.Question:
                    if (ctx.CalloutRunning && !string.IsNullOrEmpty(ctx.CalloutName))
                        return Pick(rng,
                            unit + ", I show you on the " + ctx.CalloutName + ". Say again if that doesn't match.",
                            unit + ", I have you assigned to the " + ctx.CalloutName + ".",
                            unit + ", your call is the " + ctx.CalloutName + ". Anything else, go ahead.");
                    return Pick(rng,
                        unit + ", I show nothing outstanding on you. Say again?",
                        unit + ", nothing on your unit. If you have something, put it out.",
                        unit + ", negative on that. Advise what you need.");

                case DispatcherIntent.Instruction:
                    return Pick(rng,
                        "Copy " + unit + ", working on it.",
                        "10-4 " + unit + ", stand by.",
                        "Copy. I'll get back to you, " + unit + ".");

                case DispatcherIntent.Status:
                    return Pick(rng,
                        "10-4 " + unit + ".",
                        "Copy " + unit + ".",
                        unit + ", 10-4.");
            }

            return Pick(rng,
                "Copy " + unit + ".",
                "10-4 " + unit + ". Go ahead.",
                unit + ", say again? You broke up.",
                "Copy that, " + unit + ".");
        }

        private static string Pick(Random rng, params string[] options)
        {
            return options[rng.Next(options.Length)];
        }

        // ------------------------------------------------------------------ model prompt

        public static void BuildPrompt(DispatchContext ctx, string transcript, string playerLine, out string system, out string user)
        {
            var sb = new StringBuilder();
            sb.Append("You are the police radio dispatcher for the Los Santos Police Department.\n");
            sb.Append(ctx.Describe()).Append("\n");
            sb.Append("Rules:\n");
            sb.Append("- Reply with ONE short radio transmission, 25 words or fewer.\n");
            sb.Append("- Speak like a dispatcher: clipped, procedural, calm. Use 10-codes where they fit.\n");
            sb.Append("- Address the unit as ").Append(ctx.Unit).Append(".\n");
            sb.Append("- Only use the facts above. Never invent units, calls, warrants, plates or outcomes.\n");
            sb.Append("- Never say units are responding unless you were told they are.\n");
            sb.Append("- If you do not know, ask the unit to repeat or to advise.\n");
            sb.Append("- No narration, no stage directions, no quotation marks. Never mention being an AI.\n");
            system = sb.ToString();

            var ub = new StringBuilder();
            if (!string.IsNullOrEmpty(transcript)) ub.Append(transcript).Append("\n");
            ub.Append("Unit ").Append(ctx.Unit).Append(" transmits: ").Append(playerLine).Append("\n");
            ub.Append("Reply as dispatch:");
            user = ub.ToString();
        }
    }
}
