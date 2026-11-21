namespace AudioLab.Core.Model;

/// <summary>One drum-machine lane: a sample and its step pattern.</summary>
public sealed record BeatLane
{
    /// <summary>The lane's id.</summary>
    public required BeatLaneId Id { get; init; }

    /// <summary>The lane's name.</summary>
    public required string Name { get; init; }

    /// <summary>The sample the lane plays, or null while it has none.</summary>
    public MediaId? Media { get; init; }

    /// <summary>The lane gain, from 0 up; 1 is unity.</summary>
    public required double Gain { get; init; }

    /// <summary>Which steps fire; its length equals the pattern's step count.</summary>
    public required IReadOnlyList<bool> Steps { get; init; }
}
