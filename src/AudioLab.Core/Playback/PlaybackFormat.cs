namespace AudioLab.Core.Playback;

/// <summary>The fixed numbers of the page's engine: how the worker chunks audio and how much buffering it may add up to (D24).</summary>
public static class PlaybackFormat
{
    /// <summary>Frames in each chunk the worker renders: 10.7 ms at 48 kHz.</summary>
    public const int ChunkFrames = 512;

    /// <summary>The most output buffering, in milliseconds, the worklet's ring and the audio context's own latency may add up to.</summary>
    public const int BudgetMilliseconds = 100;
}
