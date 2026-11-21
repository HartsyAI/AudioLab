namespace AudioLab.Core.Model;

/// <summary>What text a document may hold: no control characters and none that reorder what is shown.</summary>
public static class TextRules
{
    /// <summary>Whether <paramref name="text"/> has no control characters and no text-direction marks.</summary>
    /// <param name="text">The text to check.</param>
    /// <param name="allowLineBreaks">Whether tab, line feed and carriage return are allowed, as in lyrics.</param>
    public static bool IsClean(string text, bool allowLineBreaks = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (char c in text)
        {
            bool lineBreak = c is '\t' or '\n' or '\r';
            if ((char.IsControl(c) && !(allowLineBreaks && lineBreak)) || IsDirectionControl(c))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsDirectionControl(char c)
    {
        return c is '؜' or '‎' or '‏' or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩');
    }
}
