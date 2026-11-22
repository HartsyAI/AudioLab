using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Renames a section.</summary>
public sealed record RenameSection : Command
{
    /// <summary>The section to rename.</summary>
    public required SectionId Section { get; init; }

    /// <summary>The new name; not blank.</summary>
    public required string Name { get; init; }
}
