using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>Turns the SwarmUI-AudioLab extension's version 3 project JSON into a version 4 document.</summary>
public static class ProjectMigration
{
    /// <summary>The oldest project version this migration reads.</summary>
    public const int OldestVersion = 3;

    /// <summary>Migrates a version 3 project; the same JSON, owner and ids always give the same result.</summary>
    /// <remarks>A refusal part way through may have used some ids; that is harmless, as no id is handed out twice.</remarks>
    /// <param name="json">The project file as the extension saved it.</param>
    /// <param name="owner">The user the project now belongs to, from the door it arrived through.</param>
    /// <param name="ids">The service every new id comes from.</param>
    /// <param name="audio">The audio files of a project whose JSON lists none, such as an autosave; otherwise null.</param>
    /// <exception cref="ProjectFormatException">The text is not a version 3 project, or version 4 cannot hold it.</exception>
    public static MigrationResult FromV3(
        string json,
        UserId owner,
        IIdService ids,
        IReadOnlyDictionary<string, MediaFileInfo>? audio = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(ids);
        if (!IdRules.IsValid(owner.Value))
        {
            throw new ArgumentException("A migrated project needs a well-formed owner.", nameof(owner));
        }
        V3Project project = Read(ProjectVersion.WithoutByteOrderMark(json));
        RequireVersion3(project.Version);
        List<string> warnings = [];
        V3Context context = new(ids, V3Audio.From(project, audio, warnings), warnings);
        ProjectDocument document = V3Migrator.Migrate(project, owner, context);
        IReadOnlyList<string> problems = ProjectValidator.Validate(document);
        if (problems.Count > 0)
        {
            throw new ProjectFormatException("The migrated project breaks the version 4 rules:", problems);
        }
        return new MigrationResult { Document = document, Media = context.MediaReferences, Warnings = warnings };
    }

    private static void RequireVersion3(int? version)
    {
        switch (version)
        {
            case null:
                throw new ProjectFormatException("The project has no version, so it cannot be migrated.");
            case < OldestVersion:
                throw new ProjectFormatException(
                    $"Version {version} projects are older than version {OldestVersion} and cannot be migrated.");
            case ProjectDocument.CurrentVersion:
                throw new ProjectFormatException("The project is already version 4; read it with ProjectJson.Read.");
            case not OldestVersion:
                throw new ProjectFormatException($"Version {version} is newer than this migration understands.");
        }
    }

    // One pass over the text on success. Only when it fails is the version looked at, so a version 2 or version 4
    // file gets a message about its version and not about the first field this reader does not know.
    private static V3Project Read(string text)
    {
        try
        {
            return JsonSerializer.Deserialize(text, V3JsonContext.Default.V3Project)
                ?? throw new ProjectFormatException("The project file is empty.");
        }
        catch (JsonException exception)
        {
            RequireVersion3(ProjectVersion.Peek(text));
            throw new ProjectFormatException(
                "The version 3 project has a field or value this migration cannot preserve.",
                [JsonErrors.Describe(exception)],
                exception);
        }
    }
}
