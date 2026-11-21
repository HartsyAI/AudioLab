namespace AudioLab.Core.Model;

/// <summary>A waveshaping saturator.</summary>
public sealed record SaturationEffect : Effect
{
    /// <summary>The drive amount, 0 to 1.</summary>
    public required double Drive { get; init; }

    /// <summary>The output trim, in decibels.</summary>
    public required double Trim { get; init; }

    /// <summary>The wet share, 0 to 1.</summary>
    public required double Mix { get; init; }
}
