namespace AudioLab.Web.Bench;

/// <summary>Everything one scripted run measured, with the facts needed to say what it was measured on.</summary>
public sealed record BenchReport
{
    /// <summary>Label given in the address, for example the build flavour.</summary>
    public required string Label { get; init; }

    /// <summary>The candidate measured.</summary>
    public required string Renderer { get; init; }

    /// <summary>The browser's user agent.</summary>
    public required string UserAgent { get; init; }

    /// <summary>Device pixels per CSS pixel.</summary>
    public required double DevicePixelRatio { get; init; }

    /// <summary>Window inner width in CSS pixels.</summary>
    public required double WindowWidth { get; init; }

    /// <summary>Window inner height in CSS pixels.</summary>
    public required double WindowHeight { get; init; }

    /// <summary>Width of one bar in pixels.</summary>
    public required double BarWidth { get; init; }

    /// <summary>Tracks in the session.</summary>
    public required int Tracks { get; init; }

    /// <summary>Clips in the session.</summary>
    public required int Clips { get; init; }

    /// <summary>Width of the visible lane area.</summary>
    public required double ViewWidth { get; init; }

    /// <summary>Height of the visible lane area.</summary>
    public required double ViewHeight { get; init; }

    /// <summary>Surface the last Skia paint received, when Skia was measured.</summary>
    public required string SkiaSurface { get; init; }

    /// <summary>Fonts Skia failed to decode.</summary>
    public required string[] FontFailures { get; init; }

    /// <summary>One entry per scenario run.</summary>
    public required FrameStats[] Results { get; init; }
}
