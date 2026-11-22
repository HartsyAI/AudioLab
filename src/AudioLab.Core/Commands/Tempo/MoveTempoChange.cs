using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Moves a tempo change in time; it keeps its tempo.</summary>
public sealed record MoveTempoChange : Command
{
    /// <summary>The tempo change to move.</summary>
    [Description("The tempo change to move.")]
    public required TempoChangeId Change { get; init; }

    /// <summary>Where it starts, in seconds; a time below 0.01 becomes 0.01.</summary>
    [Description("Where it starts, in seconds; a time below 0.01 becomes 0.01.")]
    public required double Time { get; init; }
}
