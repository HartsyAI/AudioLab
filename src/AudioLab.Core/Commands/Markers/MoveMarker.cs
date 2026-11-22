using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Moves a marker in time.</summary>
public sealed record MoveMarker : Command
{
    /// <summary>The marker to move.</summary>
    public required MarkerId Marker { get; init; }

    /// <summary>Where it goes, in seconds.</summary>
    public required double Time { get; init; }
}
