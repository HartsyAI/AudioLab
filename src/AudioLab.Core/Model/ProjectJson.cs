using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>Reads and writes version 4 project documents. Neither direction accepts an invalid document.</summary>
public static class ProjectJson
{
    /// <summary>Reads a version 4 document and checks it.</summary>
    /// <exception cref="ProjectFormatException">The text is not a valid version 4 project.</exception>
    public static ProjectDocument Read(string json)
    {
        string text = ProjectVersion.WithoutByteOrderMark(json);
        int? version = ProjectVersion.Peek(text);
        if (version != ProjectDocument.CurrentVersion)
        {
            string found = version is { } number ? $"version {number}" : "no version";
            string expected = $"Expected a version {ProjectDocument.CurrentVersion} project";
            throw new ProjectFormatException($"{expected}, found {found}.");
        }
        ProjectDocument document;
        try
        {
            // The version check proved the text is a JSON object, so this is never the literal null.
            document = JsonSerializer.Deserialize(text, ProjectJsonContext.Default.ProjectDocument)!;
        }
        catch (JsonException exception)
        {
            throw Refused(JsonErrors.Describe(exception), exception);
        }
        catch (NotSupportedException exception)
        {
            throw Refused(ProjectLimits.Shorten(exception.Message), exception);
        }
        Check(document);
        return document;
    }

    /// <summary>Checks a document and writes it as compact JSON.</summary>
    /// <exception cref="ProjectFormatException">The document breaks a rule of the validator.</exception>
    public static string Write(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Check(document);
        return JsonSerializer.Serialize(document, ProjectJsonContext.Default.ProjectDocument);
    }

    private static ProjectFormatException Refused(string problem, Exception inner)
    {
        return new ProjectFormatException("The project is not a valid version 4 document.", [problem], inner);
    }

    private static void Check(ProjectDocument document)
    {
        IReadOnlyList<string> problems = ProjectValidator.Validate(document);
        if (problems.Count > 0)
        {
            throw new ProjectFormatException("The project breaks the version 4 rules:", problems);
        }
    }
}
