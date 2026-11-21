using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>Reads the format version of a project file without reading the rest of it.</summary>
public static class ProjectVersion
{
    /// <summary>The version of <paramref name="json"/>, or null when it has none.</summary>
    /// <exception cref="ProjectFormatException">The text is not a JSON object.</exception>
    public static int? Peek(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            VersionProbe? probe = JsonSerializer.Deserialize(json, VersionProbeContext.Default.VersionProbe);
            return probe?.Version;
        }
        catch (JsonException exception)
        {
            throw new ProjectFormatException("The project file is not a JSON object with a whole-number version.", [exception.Message], exception);
        }
    }
}
