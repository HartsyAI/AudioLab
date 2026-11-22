namespace AudioLab.Web.Session;

/// <summary>The one place seconds become pixels: a linear scale, so a clip, a marker and a section sit where their seconds put them.</summary>
/// <remarks>Bars are not uniform in seconds once the tempo changes, so the ruler asks <see cref="BarGrid"/> for each bar line instead of multiplying.</remarks>
/// <param name="PixelsPerSecond">How many CSS pixels one second takes.</param>
public readonly record struct TimelineGeometry(double PixelsPerSecond)
{
    /// <summary>The scale at which a bar of 4/4 at 120 beats per minute is 68 pixels, as in the design mock.</summary>
    public static TimelineGeometry Default => new(34);

    /// <summary>The pixel at which a time starts.</summary>
    public double X(double seconds) => seconds * PixelsPerSecond;

    /// <summary>How many pixels a span of seconds takes.</summary>
    public double Width(double seconds) => seconds * PixelsPerSecond;

    /// <summary>The time at a pixel.</summary>
    public double Seconds(double x) => x / PixelsPerSecond;
}
