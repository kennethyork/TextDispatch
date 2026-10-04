using System.Text;
using TextDispatch.Ai;

namespace TextDispatch.Dialogue
{
    /// <summary>
    /// How a person in the street replies, when a model is doing the talking.
    ///
    /// The prompt carries only facts about this person and this moment. The model is told what is
    /// true rather than asked to invent it, so it cannot produce a warrant that does not exist or
    /// confess to a crime the world knows nothing about - and because the reply is never parsed as
    /// a command, it has no authority even if it tries.
    ///
    /// Everything here runs on a background thread; the reply is collected on the fiber.
    /// </summary>
    internal static class NpcPrompt
    {
        public static string Ask(Settings settings, Talker talker, PedState state, string playerLine, out string error)
        {
            var system = new StringBuilder();
            system.Append("You are ").Append(talker.Name).Append(", a person in Los Santos.\n");
            system.Append("What is true right now: ").Append(state.Describe()).Append("\n");
            system.Append("Your mood: ").Append(talker.Mood.ToString().ToLowerInvariant()).Append(".\n");
            system.Append("Rules:\n");
            system.Append("- Reply with ONE short line of spoken dialogue, 25 words or fewer.\n");
            system.Append("- Write only what ").Append(talker.Name).Append(" says. No narration, no stage directions, no quotation marks.\n");
            system.Append("- Stay in character. You may lie, deflect, complain or cooperate.\n");
            system.Append("- Never mention being an AI, a model, or a game.\n");

            var user = new StringBuilder();
            var transcript = talker.Transcript(6);
            if (transcript.Length > 0) user.Append(transcript).Append("\n");
            user.Append("The officer says: ").Append(playerLine).Append("\n");
            user.Append("Reply as ").Append(talker.Name).Append(":");

            return LocalModel.Chat(settings, system.ToString(), user.ToString(), out error);
        }
    }
}
