namespace AudioLab.Core.Model;

/// <summary>Just the version of a project file, so another version is refused before it is read as this one.</summary>
internal sealed record VersionProbe
{
    /// <summary>The format version, or null when the file has none.</summary>
    public int? Version { get; init; }
}
