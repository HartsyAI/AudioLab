using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Renames a marker.</summary>
public sealed record RenameMarker : Command
{
    /// <summary>The marker to rename.</summary>
    public required MarkerId Marker { get; init; }

    /// <summary>The new name; not blank.</summary>
    public required string Name { get; init; }
}
