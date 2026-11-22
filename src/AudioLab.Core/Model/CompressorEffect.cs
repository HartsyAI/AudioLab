namespace AudioLab.Core.Model;

/// <summary>A dynamics compressor with make-up gain.</summary>
public sealed record CompressorEffect : Effect
{
    /// <summary>The level above which compression starts, in decibels.</summary>
    public required double Threshold { get; init; }

    /// <summary>The width of the soft knee, in decibels.</summary>
    public required double Knee { get; init; }

    /// <summary>The compression ratio, such as 4 for 4:1.</summary>
    public required double Ratio { get; init; }

    /// <summary>The attack time, in seconds.</summary>
    public required double Attack { get; init; }

    /// <summary>The release time, in seconds.</summary>
    public required double Release { get; init; }

    /// <summary>The make-up gain, in decibels.</summary>
    public required double Makeup { get; init; }
}
