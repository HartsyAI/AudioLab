using System.Globalization;
using System.Text;

namespace AudioLab.Audio;

/// <summary>Makes a name that is safe in a file name from text a user typed.</summary>
public static class ExportFileName
{
    /// <summary>The text with what a file name cannot hold made underscores, trimmed and cut to <paramref name="maxLength"/> characters.</summary>
    /// <remarks>
    /// Control and format characters (the ones that reorder text among them) and the characters of a path become underscores, and a long
    /// name is cut at a whole character of text, never inside a surrogate pair or a combining sequence. The result may be empty.
    /// </remarks>
    public static string Clean(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        string cleaned = new(text.Select(CleanCharacter).ToArray());
        return Cut(cleaned.Trim(), maxLength).TrimEnd();
    }

    private static char CleanCharacter(char character) =>
        character is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|'
        || char.GetUnicodeCategory(character) is UnicodeCategory.Control or UnicodeCategory.Format
            ? '_'
            : character;

    private static string Cut(string text, int length)
    {
        if (text.Length <= length)
        {
            return text;
        }
        StringBuilder cut = new();
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (cut.Length + element.Length > length)
            {
                break;
            }
            cut.Append(element);
        }
        return cut.ToString();
    }
}
