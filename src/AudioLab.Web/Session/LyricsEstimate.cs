using System.Globalization;

namespace AudioLab.Web.Session;

/// <summary>What the lyrics tab says about the estimate of a clip's timing: why it cannot be made, and what it came to.</summary>
public static class LyricsEstimate
{
    /// <summary>Why the estimate cannot be made now, or null when it can.</summary>
    /// <param name="canEdit">Whether the project can be edited.</param>
    /// <param name="reason">Why it cannot be edited, when it cannot.</param>
    /// <param name="unsaved">Whether the editor holds text that is not saved; the estimate spreads the saved words.</param>
    /// <param name="words">The number of words in the saved text.</param>
    public static string? Block(bool canEdit, string? reason, bool unsaved, int words)
    {
        if (!canEdit)
        {
            return reason;
        }
        if (unsaved)
        {
            return "Save the text first, so the estimate spreads the words you see.";
        }
        return words == 0 ? "Write some lyrics first; there are no words to spread." : null;
    }

    /// <summary>What the status line says after an estimate has spread the words over the clip.</summary>
    public static string Summary(int words, double seconds, double start)
    {
        string noun = words == 1 ? "word" : "words";
        return string.Create(CultureInfo.InvariantCulture, $"Spread {words} {noun} evenly over {seconds:0.0} seconds from {start:0.0} s.");
    }
}
