using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Moves a tempo change in time; it keeps its tempo.</summary>
public sealed record MoveTempoChange : Command
{
    /// <summary>The tempo change to move.</summary>
    public required TempoChangeId Change { get; init; }

    /// <summary>Where it starts, in seconds; at least 0.01.</summary>
    public required double Time { get; init; }
}
