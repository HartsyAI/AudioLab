using System.Text;
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
            RequireVersion(json);
            document = JsonSerializer.Deserialize(json, ProjectJsonContext.Default.ProjectDocument)
                ?? throw new ProjectFormatException("The project is empty.");
        }
        catch (JsonException exception)
        {
            throw Refused(JsonErrors.Describe(exception), exception);
        }
        catch (NotSupportedException exception)
        {
            throw Refused(JsonErrors.Describe(JsonErrors.FromNotSupported(exception)), exception);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
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

    // A file of another version is named as such before its members are read, so a newer file with members this
    // version does not know still says which version it is.
    private static void RequireVersion(string json)
    {
        if (FoundVersion(json) is { } version && version != ProjectDocument.CurrentVersion)
        {
            throw Refused(ProjectValidator.VersionProblem(version), null);
        }
    }

    // The top-level version number if there is one, read without building the whole document. Text that is not JSON
    // gives null: the serializer reads it next and reports the same mistake with its path.
    private static int? FoundVersion(string json)
    {
        try
        {
            Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json));
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isVersion = reader.ValueTextEquals("version"u8);
                reader.Read();
                if (isVersion)
                {
                    return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int version) ? version : null;
                }
                reader.Skip();
            }
        }
        catch (JsonException)
        {
            // Not a second clean-up of the message: this only declines to judge the version of a text that is not
            // JSON. The serializer then reads it and reports the syntax error with its path, and
            // JsonErrors.BaseMessage cuts the position out of that message. They do different jobs.
            return null;
        }
        return null;
    }

    private static ProjectFormatException Refused(string problem, Exception? inner)
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
