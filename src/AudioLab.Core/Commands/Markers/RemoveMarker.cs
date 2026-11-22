using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a marker.</summary>
public sealed record RemoveMarker : Command
{
    /// <summary>The marker to remove.</summary>
    public required MarkerId Marker { get; init; }
}
