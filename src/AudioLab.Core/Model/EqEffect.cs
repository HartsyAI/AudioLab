namespace AudioLab.Core.Model;

/// <summary>A five-band equalizer: high-pass, low shelf, mid peak, high shelf, low-pass.</summary>
public sealed record EqEffect : Effect
{
    /// <summary>The high-pass frequency, in hertz.</summary>
    public required double HpFreq { get; init; }

    /// <summary>The low-shelf gain, in decibels.</summary>
    public required double LowGain { get; init; }

    /// <summary>The low-shelf frequency, in hertz.</summary>
    public required double LowFreq { get; init; }

    /// <summary>The mid-peak gain, in decibels.</summary>
    public required double MidGain { get; init; }

    /// <summary>The mid-peak frequency, in hertz.</summary>
    public required double MidFreq { get; init; }

    /// <summary>The mid-peak width.</summary>
    public required double MidQ { get; init; }

    /// <summary>The high-shelf gain, in decibels.</summary>
    public required double HighGain { get; init; }

    /// <summary>The high-shelf frequency, in hertz.</summary>
    public required double HighFreq { get; init; }

    /// <summary>The low-pass frequency, in hertz.</summary>
    public required double LpFreq { get; init; }
}
