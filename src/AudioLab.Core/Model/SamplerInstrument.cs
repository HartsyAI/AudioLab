namespace AudioLab.Core.Model;

/// <summary>A sampler that plays one media file across the keyboard.</summary>
public sealed record SamplerInstrument : Instrument
{
    /// <summary>The instrument's name.</summary>
    public required string Name { get; init; }

    /// <summary>The media file played, or null while none is chosen.</summary>
    public MediaId? Sample { get; init; }

    /// <summary>The sample's display name.</summary>
    public required string SampleName { get; init; }

    /// <summary>The MIDI note at which the sample plays at its original pitch.</summary>
    public required int RootNote { get; init; }

    /// <summary>The attack time, in seconds.</summary>
    public required double Attack { get; init; }

    /// <summary>The release time, in seconds.</summary>
    public required double Release { get; init; }

    /// <summary>The output level, 0 to 1.</summary>
    public required double Gain { get; init; }

    /// <summary>Whether a note plays the whole sample regardless of how long it is held.</summary>
    public required bool OneShot { get; init; }
}
