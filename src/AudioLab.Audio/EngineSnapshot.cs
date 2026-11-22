namespace AudioLab.Audio;

/// <summary>What the render meter had measured at one moment, for reports.</summary>
public sealed record EngineSnapshot
{
    /// <summary>Render calls so far.</summary>
    public required long Calls { get; init; }

    /// <summary>Frames rendered so far.</summary>
    public required long Frames { get; init; }

    /// <summary>The longest call, in milliseconds.</summary>
    public required double MaxMs { get; init; }

    /// <summary>The call duration 50% of calls stayed under, to a quarter millisecond.</summary>
    public required double P50Ms { get; init; }

    /// <summary>The call duration 99% of calls stayed under, to a quarter millisecond.</summary>
    public required double P99Ms { get; init; }

    /// <summary>Rendering time over audio time; 1 is a whole core.</summary>
    public required double Utilisation { get; init; }

    /// <summary>Reads a meter.</summary>
    public static EngineSnapshot From(RenderLoad load)
    {
        ArgumentNullException.ThrowIfNull(load);
        return new EngineSnapshot
        {
            Calls = load.Calls,
            Frames = load.Frames,
            MaxMs = load.MaxMilliseconds,
            P50Ms = load.PercentileMilliseconds(0.5),
            P99Ms = load.PercentileMilliseconds(0.99),
            Utilisation = load.Utilisation(),
        };
    }
}
