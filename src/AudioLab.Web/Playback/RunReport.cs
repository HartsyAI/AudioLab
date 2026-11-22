using AudioLab.Audio;

namespace AudioLab.Web.Playback;

/// <summary>One report of a run, posted to the test server while it goes and once when it ends.</summary>
public sealed record RunReport
{
    /// <summary>The label given in the query string.</summary>
    public required string Label { get; init; }

    /// <summary>Where the engine runs.</summary>
    public required string Mode { get; init; }

    /// <summary>The run's phase: starting, running, done or failed.</summary>
    public required string Phase { get; init; }

    /// <summary>Whether this is the last report.</summary>
    public required bool Final { get; init; }

    /// <summary>The browser's user agent.</summary>
    public required string UserAgent { get; init; }

    /// <summary>Whether the page was cross-origin isolated.</summary>
    public required bool CrossOriginIsolated { get; init; }

    /// <summary>What <c>typeof SharedArrayBuffer</c> said.</summary>
    public required string SharedArrayBufferType { get; init; }

    /// <summary>Whether the page ran inside a frame.</summary>
    public required bool InFrame { get; init; }

    /// <summary>The context's sample rate.</summary>
    public required double SampleRate { get; init; }

    /// <summary>The context's base latency in milliseconds.</summary>
    public required double BaseLatencyMs { get; init; }

    /// <summary>The context's output latency in milliseconds; 0 where the browser does not report it.</summary>
    public required double OutputLatencyMs { get; init; }

    /// <summary>The output buffering allowed, in milliseconds, including the two latencies.</summary>
    public required double BufferBudgetMs { get; init; }

    /// <summary>The ring's size in frames.</summary>
    public required int CapacityFrames { get; init; }

    /// <summary>The ring's size in milliseconds.</summary>
    public required double CapacityMs { get; init; }

    /// <summary>The most the ring held, in milliseconds.</summary>
    public required double MaxFillMs { get; init; }

    /// <summary>Frames in a chunk.</summary>
    public required int ChunkFrames { get; init; }

    /// <summary>Wall-clock seconds since the run started.</summary>
    public required double WallSeconds { get; init; }

    /// <summary>The audio clock's seconds since the run started, from the context's frame counter.</summary>
    public required double AudioSeconds { get; init; }

    /// <summary>Whether the page was visible.</summary>
    public required string Visibility { get; init; }

    /// <summary>Whether the page had the keyboard focus.</summary>
    public required bool HasFocus { get; init; }

    /// <summary>The managed heap of the page's runtime, in bytes.</summary>
    public required long HeapBytes { get; init; }

    /// <summary>The audio thread's counters.</summary>
    public required WorkletStats Worklet { get; init; }

    /// <summary>The engine's render timing, from whichever thread hosts it.</summary>
    public EngineSnapshot? EngineLoad { get; init; }

    /// <summary>What loading the clips cost.</summary>
    public ClipLoadResult? Clips { get; init; }

    /// <summary>The browser's own underrun statistics, where it has them.</summary>
    public string? PlaybackStats { get; init; }

    /// <summary>What the drag and scroll load did to the page; null when there was none.</summary>
    public LoadSummary? Load { get; init; }

    /// <summary>The milliseconds of the stall requested, 0 for none.</summary>
    public required int StallMilliseconds { get; init; }

    /// <summary>The error, when the run failed.</summary>
    public string? Error { get; init; }
}
