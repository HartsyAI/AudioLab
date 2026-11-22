namespace AudioLab.Core.Commands;

/// <summary>Adds a tempo change; the server allocates its id.</summary>
public sealed record AddTempoChange : Command
{
    /// <summary>Where it starts, in seconds; at least 0.01.</summary>
    public required double Time { get; init; }

    /// <summary>The tempo from there on, in beats per minute.</summary>
    public required double Bpm { get; init; }
}
