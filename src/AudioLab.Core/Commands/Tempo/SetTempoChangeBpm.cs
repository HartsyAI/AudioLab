using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the tempo of one tempo change.</summary>
public sealed record SetTempoChangeBpm : Command
{
    /// <summary>The tempo change to edit.</summary>
    public required TempoChangeId Change { get; init; }

    /// <summary>The tempo from its start on, in beats per minute.</summary>
    public required double Bpm { get; init; }
}
