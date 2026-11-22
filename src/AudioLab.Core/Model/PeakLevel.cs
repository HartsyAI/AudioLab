namespace AudioLab.Core.Model;

/// <summary>One zoom level of a peak file: the lowest and highest sample of each run of frames, for every channel.</summary>
public sealed class PeakLevel
{
    private readonly short[] _values;

    /// <summary>Creates a level from interleaved values: for each peak, for each channel, the minimum then the maximum.</summary>
    public PeakLevel(int framesPerPeak, short[] values)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(framesPerPeak, 1);
        ArgumentNullException.ThrowIfNull(values);
        FramesPerPeak = framesPerPeak;
        _values = values;
    }

    /// <summary>How many frames of the file each peak covers; the last peak may cover fewer.</summary>
    public int FramesPerPeak { get; }

    /// <summary>The peaks as signed 16-bit values: for each peak, for each channel, the minimum then the maximum.</summary>
    public ReadOnlySpan<short> Values => _values;

    /// <summary>How many peaks the level holds for a file of <paramref name="channels"/> channels.</summary>
    public int PeakCount(int channels) => _values.Length / (channels * 2);
}
