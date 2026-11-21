namespace AudioLab.Core.Model;

/// <summary>What text a document may hold: no control characters, no unpaired surrogates, nothing that reorders it.</summary>
public static class TextRules
{
    /// <summary>Whether <paramref name="text"/> has no control characters, no unpaired surrogates and no direction marks.</summary>
    /// <param name="text">The text to check.</param>
    /// <param name="allowLineBreaks">Whether tab, line feed and carriage return are allowed, as in lyrics.</param>
    public static bool IsClean(string text, bool allowLineBreaks = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool lineBreak = c is '\t' or '\n' or '\r';
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(c) || (char.IsControl(c) && !(allowLineBreaks && lineBreak)) || IsDirectionControl(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Whether every surrogate in <paramref name="text"/> is half of a valid pair.</summary>
    public static bool IsWellFormed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return false;
            }
        }
        return true;
    }

    // U+061C, U+200E, U+200F, U+202A to U+202E and U+2066 to U+2069.
    private static bool IsDirectionControl(char c)
    {
        return c is '\u061C' or '\u200E' or '\u200F' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');
    }
}
