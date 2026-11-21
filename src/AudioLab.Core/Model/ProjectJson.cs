using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>Reads and writes version 4 project documents. Neither direction accepts an invalid document.</summary>
public static class ProjectJson
{
    /// <summary>Reads a version 4 document and checks it.</summary>
    /// <exception cref="ProjectFormatException">The text is not a valid version 4 project.</exception>
    public static ProjectDocument Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        int? version = ProjectVersion.Peek(json);
        if (version != ProjectDocument.CurrentVersion)
        {
            string found = version is { } number ? $"version {number}" : "no version";
            throw new ProjectFormatException($"Expected a version {ProjectDocument.CurrentVersion} project, found {found}.");
        }
        ProjectDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, ProjectJsonContext.Default.ProjectDocument);
        }
        catch (JsonException exception)
        {
            throw new ProjectFormatException("The project is not a valid version 4 document.", [Describe(exception)], exception);
        }
        if (document is null)
        {
            throw new ProjectFormatException("The project file is empty.");
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

    private static string Describe(JsonException exception)
    {
        bool hasPath = exception.Message.Contains("Path:", StringComparison.Ordinal);
        return exception.Path is { } path && !hasPath ? $"{path}: {exception.Message}" : exception.Message;
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
