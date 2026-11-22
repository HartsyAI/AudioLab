namespace AudioLab.Audio;

/// <summary>How a render is written to a WAV file.</summary>
public sealed record WavOptions
{
    /// <summary>How the samples are stored.</summary>
    public WavFormat Format { get; init; } = WavFormat.Pcm24;

    /// <summary>The dither used when the samples are rounded to integers; a float file has no rounding and ignores it.</summary>
    public DitherKind Dither { get; init; } = DitherKind.Triangular;

    /// <summary>Seeds the dither noise, so the same render always gives the same file.</summary>
    public uint Seed { get; init; } = 1;
}
