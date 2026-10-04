using System.Diagnostics;
using System.Text;
using TextDispatch.Ai;

namespace TextDispatch.Dialogue
{
    /// <summary>
    /// How a person in the street replies, when a model is doing the talking.
    ///
    /// This is the difference between a lookup and a conversation. The prompt carries **who this person
    /// is** - their age, job, district and history, read from the same records the terminal reads - what
    /// is true right now, and as much of the exchange so far as the setting allows. The model is told to
    /// keep the exchange going rather than answer in one clipped line.
    ///
    /// The guardrails are unchanged, and they are what makes a longer reply safe: the prompt contains
    /// only what this person could know, the reply is never parsed as a command, and nothing it says can
    /// change the world. Length was never the risk - authority was.
    /// </summary>
    internal static class NpcPrompt
    {
        public static string Ask(Settings settings, Talker talker, PedState state, string character,
                                 string playerLine, out string error)
        {
            var system = new StringBuilder();

            system.Append("You are ").Append(talker.Name).Append(", an ordinary person in Los Santos.\n");
            if (!string.IsNullOrEmpty(character)) system.Append("About you: ").Append(character).Append("\n");
            system.Append("What is true right now: ").Append(state.Describe()).Append("\n");
            system.Append("Your mood: ").Append(talker.Mood.ToString().ToLowerInvariant()).Append(".\n\n");

            system.Append("A police officer is talking to you, and you are in the middle of that conversation.\n");
            system.Append("Rules:\n");
            system.Append("- Reply as ").Append(talker.Name).Append(", in the first person. One or two sentences, up to about 45 words.\n");
            system.Append("- Answer what was asked. You may also deflect, lie, complain or ask something back - keep the exchange going.\n");
            system.Append("- Never repeat a line you have already used in this conversation.\n");
            system.Append("- Write only what you say. No narration, no actions, no stage directions, no quotation marks, and do not write your own name as a label.\n");
            system.Append("- Use only what you know from the conversation and the notes above. Do not invent people, events or places.\n");
            system.Append("- Never mention being an AI, a model, or a game.\n");

            var user = new StringBuilder();
            var transcript = talker.Transcript(settings.AiHistoryLines);
            if (transcript.Length > 0) user.Append("The conversation so far:\n").Append(transcript).Append("\n\n");

            user.Append("The officer says: ").Append(playerLine).Append("\n");
            user.Append("Reply as ").Append(talker.Name).Append(", in character:");

            var watch = Stopwatch.StartNew();
            var reply = LocalModel.Chat(settings, system.ToString(), user.ToString(), out error);
            watch.Stop();

            // Worth knowing when a slow machine is close to the deadline: that is exactly how a
            // conversation ends up falling back to the one-line script and feels shallow again.
            if (watch.ElapsedMilliseconds > 6000)
                Log.Line("npc model: reply took " + watch.ElapsedMilliseconds + "ms (deadline " +
                         settings.AiTimeoutMs + "ms) - raise AiTimeoutMs if replies keep falling back");

            return reply;
        }
    }
}
