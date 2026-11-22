using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Moves a marker in time.</summary>
public sealed record MoveMarker : Command
{
    /// <summary>The marker to move.</summary>
    [Description("The marker to move.")]
    public required MarkerId Marker { get; init; }

    /// <summary>Where it goes, in seconds; a time before the start becomes 0.</summary>
    [Description("Where it goes, in seconds; a time before the start becomes 0.")]
    public required double Time { get; init; }
}
