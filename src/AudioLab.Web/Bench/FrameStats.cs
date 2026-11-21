namespace AudioLab.Web.Bench;

/// <summary>What one scripted scenario measured. Frame intervals are between consecutive animation frames.</summary>
public sealed record FrameStats
{
    /// <summary>Scenario name.</summary>
    public required string Scenario { get; init; }

    /// <summary>Frames recorded.</summary>
    public required int Frames { get; init; }

    /// <summary>Frames per second over the whole run.</summary>
    public required double MeanFps { get; init; }

    /// <summary>Median frame interval in milliseconds.</summary>
    public required double MedianMs { get; init; }

    /// <summary>95th percentile frame interval in milliseconds.</summary>
    public required double P95Ms { get; init; }

    /// <summary>99th percentile frame interval in milliseconds.</summary>
    public required double P99Ms { get; init; }

    /// <summary>Longest frame interval in milliseconds.</summary>
    public required double MaxMs { get; init; }

    /// <summary>Frames whose interval was over 22.2 ms, the 45 fps line.</summary>
    public required int FramesOver45FpsLine { get; init; }

    /// <summary>Lowest frame rate over any one-second window.</summary>
    public required double WorstSecondFps { get; init; }

    /// <summary>Mean time spent in C# drawing or rendering per frame, in milliseconds.</summary>
    public required double DrawMeanMs { get; init; }

    /// <summary>95th percentile of the time spent drawing per frame.</summary>
    public required double DrawP95Ms { get; init; }

    /// <summary>Longest time spent drawing in one frame.</summary>
    public required double DrawMaxMs { get; init; }

    /// <summary>Mean interop calls per drawn frame, where the candidate counts them.</summary>
    public required double CallsPerFrame { get; init; }

    /// <summary>Mean clips painted per drawn frame, where the candidate counts them.</summary>
    public required double ClipsPerFrame { get; init; }
}
