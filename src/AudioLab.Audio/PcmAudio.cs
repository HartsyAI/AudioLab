namespace AudioLab.Audio;

/// <summary>Decoded float PCM, one array per channel. The engine reads it and never changes it, and neither may the owner while it plays.</summary>
public sealed class PcmAudio
{
    private readonly float[][] _channels;

    /// <summary>Wraps the channels, which must be one to <see cref="AudioFormat.MaxChannels"/> arrays of the same length.</summary>
    public PcmAudio(int sampleRate, float[][] channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, AudioFormat.MinSampleRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, AudioFormat.MaxSampleRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels.Length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels.Length, AudioFormat.MaxChannels);
        foreach (float[] channel in channels)
        {
            ArgumentNullException.ThrowIfNull(channel);
            if (channel.Length != channels[0].Length)
            {
                throw new ArgumentException("Every channel must hold the same number of frames.", nameof(channels));
            }
        }
        SampleRate = sampleRate;
        _channels = channels;
    }

    /// <summary>Samples per second in each channel.</summary>
    public int SampleRate { get; }

    /// <summary>How many channels the audio has.</summary>
    public int ChannelCount => _channels.Length;

    /// <summary>How many frames each channel holds.</summary>
    public int Frames => _channels[0].Length;

    /// <summary>The samples of one channel.</summary>
    public ReadOnlySpan<float> Channel(int index) => _channels[index];
}
