using System.Text.Json;

namespace AudioLab.Core.Model;

/// <summary>Reads and writes version 4 project documents. Neither direction accepts an invalid document.</summary>
public static class ProjectJson
{
    /// <summary>Reads a version 4 document and checks it; the validator refuses any other version.</summary>
    /// <exception cref="ProjectFormatException">The text is not a valid version 4 project.</exception>
    public static ProjectDocument Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        ProjectDocument document;
        try
        {
            document = JsonSerializer.Deserialize(json, ProjectJsonContext.Default.ProjectDocument)
                ?? throw new ProjectFormatException("The project is empty.");
        }
        catch (JsonException exception)
        {
            throw Refused(JsonErrors.Describe(exception), exception);
        }
        catch (NotSupportedException exception)
        {
            throw Refused(JsonErrors.Clean(exception.Message), exception);
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
