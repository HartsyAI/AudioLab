namespace AudioLab.Audio;

/// <summary>The O1 test session: sixteen tracks, each with two filter sections, a pan and a reverb send.</summary>
public static class StressMix
{
    /// <summary>Tracks in the session.</summary>
    public const int TrackCount = 16;

    /// <summary>Builds the session; odd tracks play the given clips in turn, when there are any, and the rest are sines.</summary>
    public static AudioEngine Build(int sampleRate, int blockFrames, IReadOnlyList<SampleClip> clips)
    {
        ArgumentNullException.ThrowIfNull(clips);
        AudioEngine engine = new(sampleRate, blockFrames);
        int[] scale = [0, 3, 5, 7, 10, 12, 15, 17];
        for (int i = 0; i < TrackCount; i++)
        {
            ITrackSource source = i % 2 == 1 && clips.Count > 0
                ? new SamplePlayer(clips[i / 2 % clips.Count], sampleRate)
                : new SineSource(55 * Math.Pow(2, ((scale[i % scale.Length] + (12 * (i / 8))) / 12.0) + 1), sampleRate);
            Track track = engine.AddTrack(source, 1200 + (300 * i));
            track.Pan = (((i * 5) % 16) - 7.5f) / 8f;
            track.Gain = 0.45f;
            track.ReverbSend = 0.1f + (0.02f * i);
        }
        engine.MasterGain = 0.1f;
        return engine;
    }
}
