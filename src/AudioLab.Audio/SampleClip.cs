namespace AudioLab.Audio;

/// <summary>Stereo 16-bit audio held in memory, interleaved.</summary>
public sealed class SampleClip
{
    /// <summary>Creates a clip from interleaved stereo samples.</summary>
    public SampleClip(short[] samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        if (samples.Length == 0 || samples.Length % 2 != 0)
        {
            throw new ArgumentException("A clip holds whole stereo frames.", nameof(samples));
        }
        Samples = samples;
        SampleRate = sampleRate;
    }

    /// <summary>The interleaved samples.</summary>
    public short[] Samples { get; }

    /// <summary>Frames per second.</summary>
    public int SampleRate { get; }

    /// <summary>Number of stereo frames.</summary>
    public int Frames => Samples.Length / 2;
}
