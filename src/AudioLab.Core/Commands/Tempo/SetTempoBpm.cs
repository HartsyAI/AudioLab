namespace AudioLab.Core.Commands;

/// <summary>Sets the tempo from the start of the project until the first tempo change.</summary>
public sealed record SetTempoBpm : Command
{
    /// <summary>The tempo, in beats per minute.</summary>
    public required double Bpm { get; init; }
}
