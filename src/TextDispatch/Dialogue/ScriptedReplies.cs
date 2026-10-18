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
            // The questions of a traffic stop, before the general ones they would otherwise fall into.
            if (Has(t, "mind if i", "search your", "search the car", "search the vehicle", "can i search", "consent", "take a look in", "have a look in", "look inside"))
                return DialogueIntent.SearchConsent;
            if (Has(t, "whose car", "whose vehicle", "your car", "your vehicle", "own this", "own the car", "registered to", "who owns", "this car yours", "car belong"))
                return DialogueIntent.WhoseCar;
            if (Has(t, "drinking", "been drinking", "had anything to drink", "had a drink", "any drinks", "alcohol", "beers", "how much have you had", "under the influence", "intoxicated", "been using"))
                return DialogueIntent.Drinking;
            if (Has(t, "where are you going", "where are you headed", "where you headed", "where you going", "where are you coming from", "where you coming from",
                    "where have you been", "where you been", "on your way to", "heading to", "headed to"))
                return DialogueIntent.Destination;
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

        /// <summary>
        /// Whether they let the car be searched. Somebody with nothing to hide usually does; somebody with
        /// something does not, unless they are the kind who cannot say no to a uniform. Decided once.
        /// </summary>
        public static bool Consents(Talker talker, PedState state)
        {
            var roll = new Random(unchecked(talker.Handle * 503 + (state != null && state.AskedAboutPerson ? 29 : 11))).Next(100);
            // What they are asked about is what they weigh: their pockets, or their car.
            var hiding = state != null && (state.AskedAboutPerson ? state.PocketsHide : state.CarHidesSomething);
            switch (talker.Mood)
            {
                case Temperament.Compliant: return hiding ? roll < 35 : roll < 95;
                case Temperament.Nervous: return hiding ? roll < 20 : roll < 80;
                case Temperament.Defensive: return hiding ? roll < 5 : roll < 45;
                default: return !hiding && roll < 20;
            }
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
                case DialogueIntent.SearchConsent:
                    return Consents(talker, state)
                        ? Pick(rng, mood, state,
                            new[] { "Go ahead. Nothing in there.", "Sure, knock yourself out.", "Yeah, that's fine." },
                            new[] { "I - I guess? There's nothing in there.", "Okay. Yeah. Sure.", "If you have to." },
                            new[] { "What for? ...Fine. Whatever.", "Fine. Make it quick.", "Go on then, you won't find anything." },
                            new[] { "Go ahead, waste your time.", "Do what you want.", "Whatever." })
                        : Pick(rng, mood, state,
                            new[] { "I'd rather you didn't, if that's okay.", "Do you need to? I'm in a hurry.", "No, I don't think so." },
                            new[] { "Why? There's nothing - do you have to?", "I - no. No, I don't want you to.", "Can I say no? I'm saying no." },
                            new[] { "No. I don't consent to any searches.", "Not without a warrant.", "No. Am I free to go?" },
                            new[] { "Get a warrant.", "Hell no.", "Touch my car and see what happens." });

                case DialogueIntent.WhoseCar:
                {
                    if (state == null || !state.HasCar)
                        return Pick(rng, mood, state,
                            new[] { "I don't have a car here.", "I walked.", "What car?" },
                            new[] { "C-car? I'm on foot.", "I don't have one.", "No car, I swear." },
                            new[] { "I don't have a car. Why?", "Do you see me in a car?", "I walked here." },
                            new[] { "What car?", "Mind your business.", "Not mine." });
                    var first = string.IsNullOrEmpty(state.CarOwner) ? "a friend" : state.CarOwner.Split(' ')[0];
                    if (state.OwnsCar)
                        return Pick(rng, mood, state,
                            new[] { "It's mine. Registered to me.", "Mine, yeah.", "My car - paperwork's in the glovebox." },
                            new[] { "It's mine! It's my car.", "Y-yes, it's mine.", "Mine. I've had it two years." },
                            new[] { "Yes, it's mine. Why?", "It's mine. Run the plate if you want.", "Whose do you think?" },
                            new[] { "Mine.", "Who else's?", "Run it." });
                    if (state.CarStolen)
                        return Pick(rng, mood, state,
                            new[] { "My friend's. He lent it to me.", "It's... a friend's car.", "Borrowed it this morning." },
                            new[] { "My cousin's! He said I could take it.", "It's - it's my friend's, I've got his number somewhere.", "I'm just driving it for someone." },
                            new[] { "A friend's. Is that a crime?", "I borrowed it. So?", "Not mine. Ask the owner." },
                            new[] { "Found it.", "Not your business.", "A friend's. Which friend? Wouldn't you like to know." });
                    return Pick(rng, mood, state,
                        new[] { "It's " + first + "'s - my cousin's. I'm borrowing it.", first + " lent it to me.", "It belongs to " + first + ", a friend." },
                        new[] { "It's - it's " + first + "'s car. My cousin.", "Borrowed it from " + first + ".", first + " said I could take it." },
                        new[] { "It's " + first + "'s. Call them.", "Belongs to " + first + ". Problem?", "Not mine. " + first + "'s." },
                        new[] { first + "'s.", "Somebody's.", "Not mine, not yours." });
                }

                case DialogueIntent.Drinking:
                {
                    var bac = state == null ? 0.0 : state.Bac;
                    if (bac <= 0.0)
                        return Pick(rng, mood, state,
                            new[] { "No, nothing. I don't really drink.", "Not a drop.", "No, officer. Stone cold sober." },
                            new[] { "No! No, nothing.", "I haven't had anything, I swear.", "N-no. Nothing." },
                            new[] { "No. Why?", "Do I look drunk?", "No. Give me a breath test if you want." },
                            new[] { "No.", "None of your business. But no.", "Breathalyse me then." });
                    if (bac < Sobriety.Limit)
                        return Pick(rng, mood, state,
                            new[] { "Just one beer with dinner, a couple of hours ago.", "I had a glass of wine. One.", "One beer. That's it." },
                            new[] { "Just - just one. Hours ago.", "One drink. I'm fine, I promise.", "A beer, with food. I'm fine." },
                            new[] { "One. I'm under the limit.", "A beer. That's legal.", "One drink. So?" },
                            new[] { "One. Big deal.", "I had a beer. Arrest me.", "Mind your business." });
                    return Pick(rng, mood, state,
                        new[] { "I had a few... but I'm fine to drive. Really.", "A couple of drinks. Three maybe.", "I've had some, I'll be honest." },
                        new[] { "No! I mean... maybe one or two.", "Just - just a couple, I'm fine.", "Only a few, I swear I'm okay." },
                        new[] { "I don't have to answer that.", "I'm not drunk, if that's what you're asking.", "Two. Maybe. I'm fine." },
                        new[] { "Not your business.", "So what if I have?", "I'm not answering that." });
                }

                case DialogueIntent.Destination:
                {
                    var where = state != null && !string.IsNullOrEmpty(state.Zone) ? state.Zone : "the city";
                    return Pick(rng, mood, state,
                        new[] { "Heading home from work.", "Picking my kid up from practice.", "Just going to the store.", "Home - I live up in " + where + "." },
                        new[] { "Home. Just home. I'm - just going home.", "To my girlfriend's. Is that okay?", "Work. I'm going to be late." },
                        new[] { "Home. Why?", "Does it matter?", "Out. Is that a problem?" },
                        new[] { "None of your business.", "Away from you.", "Wherever I want." });
                }

                case DialogueIntent.Greeting:
                    return Pick(rng, mood, state,
                        new[] { "Morning.", "Hey.", "Yeah. Hi.", "Afternoon, officer.", "What do you want?" },
                        new[] { "Uh - hello.", "H-hi.", "Hello, officer.", "Hey. Sorry, am I in trouble?" },
                        new[] { "Yeah. That's me.", "Can I help you?", "Hi. Something wrong?", "Hey." },
                        new[] { "What.", "You talking to me?", "Yeah?", "Make it quick." });

                case DialogueIntent.Identify:
                    // LSPDFR's licence says what they are handing over.
                    if (state != null && state.Licence == "unlicensed")
                        return Pick(rng, mood, state,
                            new[] { "I - don't actually have a licence. I'm sorry.", "I never got one, honestly.", "I don't have a licence." },
                            new[] { "I... left it at home. I mean - I don't have one.", "I don't have it. I don't have one.", "Please, I'm learning, I was just going round the block." },
                            new[] { "I don't have one on me.", "I don't need one for this.", "Not on me." },
                            new[] { "Don't have one.", "Don't need one.", "No." });
                    if (state != null && state.Licence == "suspended")
                        return Pick(rng, mood, state,
                            new[] { "Here. Look - it's suspended. I know. I'm sorry.", "Here you go. It might come back suspended.", "It's here. I should tell you it's suspended." },
                            new[] { "Here - is it - it's fine, right?", "H-here. Please don't run it.", "Here. It's - there might be a problem with it." },
                            new[] { "Here. Whatever it says, it's a mistake.", "There. Happy?", "It's valid. Mostly." },
                            new[] { "Take it.", "Here. Don't bother running it.", "You'll find out anyway." });
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
                    // What is in the car is on their mind, and it shows - not a confession, a tell.
                    if (state != null && state.CarHidesSomething)
                        return Pick(rng, mood, state,
                            new[] { "No... nothing like that.", "Not that I know of.", "Nothing - it's not even my stuff in there." },
                            new[] { "No! Why - why would there be?", "N-no. No weapons. Nothing.", "There's nothing. Can I go?" },
                            new[] { "Why do you keep asking?", "Nothing you have a right to look at.", "No. And you're not searching it." },
                            new[] { "You'll never know.", "Nothing for you.", "Back off my car." });
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
