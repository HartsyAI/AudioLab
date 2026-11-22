namespace AudioLab.Audio;

/// <summary>The sample rate and channel count of a stream of float PCM.</summary>
public readonly record struct AudioFormat
{
    /// <summary>Lowest sample rate the engine accepts, in hertz.</summary>
    public const int MinSampleRate = 8000;

    /// <summary>Highest sample rate the engine accepts, in hertz.</summary>
    public const int MaxSampleRate = 384000;

    /// <summary>Most channels a stream may carry.</summary>
    public const int MaxChannels = 8;

    /// <summary>Creates a format, refusing a rate or channel count the engine cannot render.</summary>
    public AudioFormat(int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, MinSampleRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, MaxSampleRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, MaxChannels);
        SampleRate = sampleRate;
        Channels = channels;
    }

    /// <summary>Samples per second in each channel.</summary>
    public int SampleRate { get; }

    /// <summary>Number of interleaved channels.</summary>
    public int Channels { get; }
}
