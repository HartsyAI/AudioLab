using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>The outcome of migrating one version 3 project.</summary>
public sealed record MigrationResult
{
    /// <summary>The version 4 document, already checked by <see cref="ProjectValidator"/>.</summary>
    public required ProjectDocument Document { get; init; }

    /// <summary>The audio files the document refers to, in order of first use; the caller stores each one.</summary>
    public required IReadOnlyList<MediaReference> Media { get; init; }

    /// <summary>Everything that was changed or dropped to make the project valid, one sentence each.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
