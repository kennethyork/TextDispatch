using System;
using System.Collections.Generic;
using System.Globalization;

namespace TextDispatch.Dialogue
{
    /// <summary>
    /// Working out what somebody was just asked, and answering in character.
    ///
    /// This is the no-setup path: it needs no model, answers instantly, and always stays inside what
    /// the world actually knows. It reads the speaker's temperament and the state LSPDFR reports,
    /// so a compliant driver who has been stopped does not answer like a hostile one who is running.
    ///
    /// It is deliberately a script and not a language model. A model is better at conversation and
    /// is supported (see LlmDialogue); this exists so that conversation works with nothing installed.
    /// </summary>
    internal static class ScriptedReplies
    {
        public static DialogueIntent Classify(string text)
        {
            var t = " " + (text ?? "").ToLowerInvariant() + " ";

            // A second copy with everything that is not a letter or digit turned into a space, so
            // single words can be matched as whole words. This exists because of a real bug found by
            // testing: the token "id " was matching inside "did ", so "why did you stop me" was
            // classified as a request for identification.
            var words = " " + new string(Array.ConvertAll(t.ToCharArray(), c => char.IsLetterOrDigit(c) ? c : ' ')) + " ";
            while (words.IndexOf("  ", StringComparison.Ordinal) >= 0)
                words = words.Replace("  ", " ");

            if (Has(t, "hello", "hey ", "hi ", "good morning", "good afternoon", "howdy", "evening")) return DialogueIntent.Greeting;
            if (HasWord(words, "license", "licence", "id", "identification", "papers", "registration") ||
                Has(t, "your name", "who are you")) return DialogueIntent.Identify;
            if (Has(t, "why did you", "why'd you", "why am i", "what's this about", "whats this about", "what is this about", "why are you stopping")) return DialogueIntent.WhyStopped;
            if (Has(t, "what happened", "whats happened", "what's happened", "did you see", "tell me what", "what did you see", "explain what")) return DialogueIntent.WhatHappened;
            if (Has(t, "weapon", "gun", "armed", "anything on you", "anything in the car", "carrying anything", "knife", "strapped")) return DialogueIntent.Weapons;
            if (Has(t, "step out", "get out", "hands where", "hands on the wheel", "on the ground", "turn around", "don't move", "dont move", "stay in the car", "keep your hands", "freeze", "get on the ground")) return DialogueIntent.Command;
            if (Has(t, "under arrest", "you're nicked", "youre nicked", "taking you in", "you are under arrest", "cuff")) return DialogueIntent.Arrest;
            if (Has(t, "calm down", "relax", "take it easy", "settle", "breathe", "no one's going to hurt", "nobodys going to hurt")) return DialogueIntent.Calm;
            if (Has(t, "speeding", "tail light", "taillight", "tail-light", "ran the light", "ran a red", "no insurance", "expired", "plate", "swerve", "drifted", "rego")) return DialogueIntent.Explain;
            if (Has(t, "last warning", "don't make me", "dont make me", "i will shoot", "stop or i", "put it down", "drop it")) return DialogueIntent.Threat;
            if (Has(t, "shut up", "idiot", "stupid", "asshole", "fuck", "pig", "bastard", "jerk")) return DialogueIntent.Insult;
            if (Has(t, "thank", "cheers", "appreciate", "have a good", "take care", "you're free", "youre free", "you can go")) return DialogueIntent.Thanks;
            if (Has(t, "how are you", "nice weather", "busy day", "quiet night", "long shift", "how's it going", "hows it going")) return DialogueIntent.SmallTalk;
            // A question does not always arrive with a question mark.
            if (t.IndexOf('?') >= 0) return DialogueIntent.Question;
            if (HasWord(words, "who", "what", "where", "when", "why", "how")) return DialogueIntent.Question;
            if (Has(t, "do you", "can you", "are you", "is there", "did you", "have you", "will you")) return DialogueIntent.Question;

            return DialogueIntent.Unknown;
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>Match whole words only, against a string already padded with spaces.</summary>
        private static bool HasWord(string paddedWords, params string[] words)
        {
            for (int i = 0; i < words.Length; i++)
                if (paddedWords.IndexOf(" " + words[i] + " ", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        public static string Answer(string playerLine, DialogueIntent intent, Talker talker, PedState state)
        {
            // Seeded on the person plus their memory length, so answers vary over a conversation but
            // a replay of the same exchange gives the same reply.
            var rng = new Random(talker.Handle * 397 + talker.Memory.Count * 13 + (int)intent * 7919);
            var mood = talker.Mood;

            // Their car has been searched. What was found is the first thing on their mind, so it is
            // what they talk about when asked anything about what happened or what they have.
            if (state != null && state.CarSearched &&
                (intent == DialogueIntent.WhatHappened || intent == DialogueIntent.Weapons ||
                 intent == DialogueIntent.Explain || intent == DialogueIntent.Question ||
                 intent == DialogueIntent.WhyStopped || intent == DialogueIntent.Arrest))
            {
                if (state.CarIllegal)
                    return Pick(rng, mood, state,
                        new[] { "Okay. Okay, that's mine. I'm not going to lie to you.", "I know how that looks. I can explain.", "Yeah... I should have told you about that." },
                        new[] { "That's not mine! I swear, I borrowed the car!", "I don't know how that got there, I swear to God.", "Please - please, that's not what it looks like." },
                        new[] { "You can't prove that's mine.", "That was there when I bought it.", "I want a lawyer before I say anything about that." },
                        new[] { "Never seen it before in my life.", "You planted that.", "I'm not saying a word about it." });

                return Pick(rng, mood, state,
                    new[] { "See? Nothing in there. Told you.", "Happy now? It's just my stuff.", "Nothing to find. Can I go?" },
                    new[] { "There's nothing, right? I told you there was nothing.", "Is - is that it? Am I okay?", "I didn't have anything, I promise." },
                    new[] { "You went through my whole car for nothing.", "Was that really necessary?", "I hope you put it all back." },
                    new[] { "Found nothing, did you?", "Waste of your time and mine.", "Told you. Now get out of my car." });
            }

            switch (intent)
            {
                case DialogueIntent.Greeting:
                    return Pick(rng, mood, state,
                        new[] { "Morning.", "Hey.", "Yeah. Hi.", "Afternoon, officer.", "What do you want?" },
                        new[] { "Uh - hello.", "H-hi.", "Hello, officer.", "Hey. Sorry, am I in trouble?" },
                        new[] { "Yeah. That's me.", "Can I help you?", "Hi. Something wrong?", "Hey." },
                        new[] { "What.", "You talking to me?", "Yeah?", "Make it quick." });

                case DialogueIntent.Identify:
                    return Pick(rng, mood, state,
                        new[] { "Yeah, sure - it's in my back pocket.", "Of course. Here you go.", "It's right here, hang on." },
                        new[] { "It's, uh - it's in the glovebox I think.", "Y-yeah. Here.", "Hold on, I know it's in here somewhere." },
                        new[] { "Why do you need my ID?", "For what, exactly?", "It's in the car. You want me to get it?" },
                        new[] { "You don't need my name.", "Ask me again and see what happens.", "I know my rights." });

                case DialogueIntent.WhyStopped:
                    return Pick(rng, mood, state,
                        new[] { "Was I? Sorry, I didn't even notice.", "Fair enough, that's on me.", "I know, I know - my fault." },
                        new[] { "I - I didn't do anything, did I?", "Was I speeding? I wasn't looking.", "Oh God. Is something wrong?" },
                        new[] { "I wasn't doing anything wrong.", "You tell me, you're the one who pulled me over.", "That's not against the law." },
                        new[] { "You tell me.", "Nothing. I did nothing.", "Whatever you say, man." });

                case DialogueIntent.WhatHappened:
                    return Pick(rng, mood, state,
                        new[] { "I just heard shouting and then someone ran off.", "I didn't see much - it happened fast.", "There was a car, and then a lot of noise." },
                        new[] { "It - it wasn't me, I swear.", "I don't know, I just got here.", "Please, I don't want any trouble." },
                        new[] { "How should I know? I was minding my own business.", "Why are you asking me?", "I wasn't involved." },
                        new[] { "Nothing happened.", "I don't know anything.", "Ask somebody else." });

                case DialogueIntent.Weapons:
                    return Pick(rng, mood, state,
                        new[] { "No, nothing. You can look.", "Just my phone and my wallet.", "Nothing on me, officer." },
                        new[] { "No! No, I - nothing.", "I don't have anything, I promise.", "No, sir. Nothing." },
                        new[] { "I've got nothing. Why?", "You going to search me for no reason?", "Nothing you need to worry about." },
                        new[] { "Wouldn't you like to know.", "Nothing you'll find.", "Keep your hands to yourself." });

                case DialogueIntent.Explain:
                    return Pick(rng, mood, state,
                        new[] { "Yeah, that's fair. Sorry.", "I know - I'll get it fixed.", "Alright, you got me." },
                        new[] { "I didn't realise. I'm sorry.", "Oh. I didn't know.", "It - it's not mine, the car's my cousin's." },
                        new[] { "That light's been out for weeks, everyone drives it.", "So?", "That's not a real problem." },
                        new[] { "So what.", "Nobody cares about that.", "Write it up then." });

                case DialogueIntent.Calm:
                    return Pick(rng, mood, state,
                        new[] { "I'm fine. I'm fine. Okay.", "Yeah. Yeah, I'm alright.", "Sorry. Long day." },
                        new[] { "I'm trying - I'm trying, okay?", "Please, I just want to go home.", "Okay. Okay. Sorry." },
                        new[] { "I am calm.", "Quit telling me to be calm.", "Don't touch me." },
                        new[] { "Back off.", "You're the one making it worse.", "Say that again." });

                case DialogueIntent.Command:
                    return Pick(rng, mood, state,
                        new[] { "Okay, okay - doing it.", "Alright, no problem.", "Right. Right, I'm moving." },
                        new[] { "Okay! Okay, I'm doing it!", "Don't - don't shoot, I'm doing it.", "I'm going, I'm going." },
                        new[] { "Fine. Fine!", "For what? What did I do?", "This is ridiculous." },
                        new[] { "Make me.", "No.", "You'd better be ready." });

                case DialogueIntent.Arrest:
                    return Pick(rng, mood, state,
                        new[] { "What? Come on - why?", "You're kidding. For what?", "Seriously? Alright... alright." },
                        new[] { "No, no no no - please, I can't go in.", "It wasn't me, I swear to God.", "Please don't do this." },
                        new[] { "On what charge?", "You can't do this.", "My lawyer's going to hear about this." },
                        new[] { "Go ahead. Try it.", "You'll regret this.", "You've got nothing on me." });

                case DialogueIntent.Threat:
                    return Pick(rng, mood, state,
                        new[] { "Okay! Okay, take it easy!", "I'm not doing anything, look at my hands!", "Alright, alright!" },
                        new[] { "Don't shoot! Please don't shoot!", "I'm putting them up! I'm putting them up!", "Okay! Okay!" },
                        new[] { "You wouldn't.", "Put that away.", "This is out of line." },
                        new[] { "Do it, then.", "You don't scare me.", "Come on, then." });

                case DialogueIntent.Insult:
                    return Pick(rng, mood, state,
                        new[] { "...Right. Was that necessary?", "Okay, that's a bit much.", "No need for that." },
                        new[] { "I - sorry, I didn't mean anything.", "I'm sorry, I'm sorry.", "Sorry. Sorry." },
                        new[] { "Real professional.", "You kiss your mother with that mouth?", "Whatever, man." },
                        new[] { "Say it again. Go on.", "Big man with a badge.", "Keep talking." });

                case DialogueIntent.Thanks:
                    return Pick(rng, mood, state,
                        new[] { "Yeah - thanks. Take care.", "Appreciate it, officer.", "Thanks. Have a good one." },
                        new[] { "Thank you. Thank you, really.", "Oh, thank God. Thanks.", "Thanks. Sorry. Thanks." },
                        new[] { "Yeah, alright.", "Fine. Whatever.", "Sure." },
                        new[] { "Whatever.", "Don't do me any favours.", "Get out of my way." });

                case DialogueIntent.SmallTalk:
                    return Pick(rng, mood, state,
                        new[] { "Quiet enough. I'm not complaining.", "Long one. You?", "Same as ever." },
                        new[] { "It's - it's been a weird night, honestly.", "Can't complain. I mean, I could.", "It's alright." },
                        new[] { "Do you always chat with people you pull over?", "It's Los Santos. It's never quiet.", "Been better." },
                        new[] { "Whatever you say.", "Don't you have criminals to chase?", "Sure." });

                case DialogueIntent.Question:
                    return Pick(rng, mood, state,
                        new[] { "I don't know. I wish I did.", "Honestly? No idea.", "That's a good question." },
                        new[] { "I - I don't know. I'm sorry.", "Please, I don't know anything.", "Why are you asking me?" },
                        new[] { "Why are you asking me that?", "Is that a real question?", "You're the police, you tell me." },
                        new[] { "No.", "None of your business.", "Don't ask me that." });
            }

            // Nothing matched. Answer as somebody interrupted mid-thought.
            return Pick(rng, mood, state,
                new[] { "Sorry - say that again?", "What?", "Hm?" },
                new[] { "S-sorry, what?", "I... what?", "Huh?" },
                new[] { "Was that a question?", "I'm listening. Barely.", "What do you want?" },
                new[] { "Speak up.", "What.", "Talking to yourself?" });
        }

        private static string Pick(Random rng, Temperament mood, PedState state, string[] compliant, string[] nervous, string[] defensive, string[] hostile)
        {
            // A person in handcuffs does not get playful, and somebody running is past talking.
            // Both are pulled from the colder sets whatever their temperament would normally be.
            if (state != null && state.InPursuit) return hostile[rng.Next(hostile.Length)];
            if (state != null && state.Arrested && mood == Temperament.Compliant)
                return defensive[rng.Next(defensive.Length)];

            switch (mood)
            {
                case Temperament.Nervous: return nervous[rng.Next(nervous.Length)];
                case Temperament.Defensive: return defensive[rng.Next(defensive.Length)];
                case Temperament.Hostile: return hostile[rng.Next(hostile.Length)];
                default: return compliant[rng.Next(compliant.Length)];
            }
        }
    }
}
