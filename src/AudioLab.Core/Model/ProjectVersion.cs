using System.Text;
using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>Reads the format version of a project file without reading the rest of it.</summary>
public static class ProjectVersion
{
    private const char ByteOrderMark = '\uFEFF';

    // The extension and this version write "version" first, so the start of the text almost always answers.
    private const int PrefixCharacters = 8192;

    /// <summary>The version of <paramref name="json"/>, or null when it has none.</summary>
    /// <exception cref="ProjectFormatException">The text is not a JSON object or its version is not a whole number.</exception>
    public static int? Peek(string json)
    {
        string text = WithoutByteOrderMark(json);
        try
        {
            bool all = text.Length <= PrefixCharacters;
            (bool found, int? version) = Scan(Encoding.UTF8.GetBytes(all ? text : text[..PrefixCharacters]), all);
            return found || all ? version : Scan(Encoding.UTF8.GetBytes(text), true).Version;
        }
        catch (JsonException exception)
        {
            string problem = JsonErrors.Describe(exception);
            throw new ProjectFormatException("The project file is not valid JSON.", [problem], exception);
        }
    }

    /// <summary>The text without a leading byte order mark, which some editors put at the start of a file.</summary>
    public static string WithoutByteOrderMark(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return json.Length > 0 && json[0] == ByteOrderMark ? json[1..] : json;
    }

    // Walks the top-level members and stops at "version". Found is false when the data ended before the answer.
    private static (bool Found, int? Version) Scan(ReadOnlySpan<byte> bytes, bool isFinal)
    {
        Utf8JsonReader reader = new(bytes, isFinal, default);
        if (!reader.Read())
        {
            return (false, null);
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new ProjectFormatException("The project file is not a JSON object.");
        }
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return (true, null);
            }
            bool isVersion = reader.ValueTextEquals("version"u8);
            if (!reader.Read())
            {
                return (false, null);
            }
            if (isVersion)
            {
                return ReadVersion(ref reader);
            }
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray && !reader.TrySkip())
            {
                return (false, null);
            }
        }
        return (false, null);
    }

    private static (bool Found, int? Version) ReadVersion(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return (true, null);
        }
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int version))
        {
            return (true, version);
        }
        const string Message = "The project file's version is not a whole number.";
        throw new ProjectFormatException(Message, ["$.version: expected a whole number."]);
    }
}
