using AudioLab.Core.Realtime;

namespace AudioLab.Web.Services;

/// <summary>Turns a refused command's reasons into a sentence for the user; the server's reasons are shown, never replaced by a guess.</summary>
public static class CommandRefusal
{
    /// <summary>The reasons of a refused reply as one sentence; a reply that carries none still says it was refused.</summary>
    public static string Describe(CommandReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        string[] reasons = [.. reply.Errors.Select(error => error.Message.Trim()).Where(message => message.Length > 0)];
        if (reasons.Length == 0)
        {
            return "The server refused the edit.";
        }
        string text = string.Join("; ", reasons);
        string sentence = char.ToUpperInvariant(text[0]) + text[1..];
        return sentence.EndsWith('.') ? sentence : sentence + ".";
    }
}
