using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>Reads the format version of a project file without reading the rest of it.</summary>
public static class ProjectVersion
{
    private const char ByteOrderMark = '\uFEFF';

    /// <summary>The version of <paramref name="json"/>, or null when it has none.</summary>
    /// <exception cref="ProjectFormatException">The text is not a JSON object.</exception>
    public static int? Peek(string json)
    {
        try
        {
            string text = WithoutByteOrderMark(json);
            return JsonSerializer.Deserialize(text, VersionProbeContext.Default.VersionProbe)?.Version;
        }
        catch (JsonException exception)
        {
            string problem = JsonErrors.Describe(exception);
            const string Message = "The project file is not a JSON object with a whole-number version.";
            throw new ProjectFormatException(Message, [problem], exception);
        }
    }

    /// <summary>The text without a leading byte order mark, which some editors put at the start of a file.</summary>
    public static string WithoutByteOrderMark(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return json.Length > 0 && json[0] == ByteOrderMark ? json[1..] : json;
    }
}
