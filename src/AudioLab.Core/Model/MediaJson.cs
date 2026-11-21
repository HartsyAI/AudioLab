using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>Reads and writes media records. Neither direction accepts an invalid record.</summary>
public static class MediaJson
{
    /// <summary>Reads a media record and checks it.</summary>
    /// <exception cref="ProjectFormatException">The text is not a valid media record.</exception>
    public static MediaRecord Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        MediaRecord media;
        try
        {
            media = JsonSerializer.Deserialize(json, ProjectJsonContext.Default.MediaRecord)
                ?? throw new ProjectFormatException("The media record is empty.");
        }
        catch (JsonException exception)
        {
            string problem = JsonErrors.Describe(exception);
            throw new ProjectFormatException("The media record is not valid.", [problem], exception);
        }
        Check(media);
        return media;
    }

    /// <summary>Checks a record and writes it as compact JSON.</summary>
    /// <exception cref="ProjectFormatException">The record breaks a rule of <see cref="MediaValidator"/>.</exception>
    public static string Write(MediaRecord media)
    {
        ArgumentNullException.ThrowIfNull(media);
        Check(media);
        return JsonSerializer.Serialize(media, ProjectJsonContext.Default.MediaRecord);
    }

    private static void Check(MediaRecord media)
    {
        IReadOnlyList<string> problems = MediaValidator.Validate(media);
        if (problems.Count > 0)
        {
            throw new ProjectFormatException("The media record breaks the rules:", problems);
        }
    }
}
