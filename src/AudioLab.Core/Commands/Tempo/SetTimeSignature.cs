namespace AudioLab.Core.Commands;

/// <summary>Sets the time signature of the project.</summary>
public sealed record SetTimeSignature : Command
{
    /// <summary>Beats per bar, 1 to 64.</summary>
    public required int Numerator { get; init; }

    /// <summary>The note value of a beat: 1, 2, 4, 8, 16 or 32.</summary>
    public required int Denominator { get; init; }
}
