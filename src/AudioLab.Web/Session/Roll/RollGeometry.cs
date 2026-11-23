namespace AudioLab.Web.Session.Roll;

/// <summary>The one place seconds and pitches become pixels in the piano roll: time across a clip's own seconds, pitch down from the highest.</summary>
/// <param name="PixelsPerSecond">How many CSS pixels one second takes.</param>
/// <param name="RowHeight">How tall one pitch is, in CSS pixels.</param>
/// <param name="Length">The clip length in seconds.</param>
public readonly record struct RollGeometry(double PixelsPerSecond, double RowHeight, double Length)
{
    /// <summary>How many pitches there are.</summary>
    public const int Pitches = 128;

    /// <summary>The scale a roll opens at: a bar of 4/4 at 120 beats per minute is 160 pixels.</summary>
    public const double DefaultPixelsPerSecond = 80;

    /// <summary>The coarsest scale.</summary>
    public const double MinPixelsPerSecond = 10;

    /// <summary>The finest scale.</summary>
    public const double MaxPixelsPerSecond = 1280;

    /// <summary>The height of a pitch row.</summary>
    public const double DefaultRowHeight = 12;

    /// <summary>The width of the drawing in pixels.</summary>
    public double Width => Length * PixelsPerSecond;

    /// <summary>The height of the drawing in pixels.</summary>
    public double Height => Pitches * RowHeight;

    /// <summary>The scale after one step of zoom: twice or half, kept between the coarsest and the finest.</summary>
    public static double Zoomed(double pixelsPerSecond, bool zoomIn)
    {
        return Math.Clamp(zoomIn ? pixelsPerSecond * 2 : pixelsPerSecond / 2, MinPixelsPerSecond, MaxPixelsPerSecond);
    }

    /// <summary>The pixel at which a time, in seconds from the clip start, falls.</summary>
    public double X(double seconds) => seconds * PixelsPerSecond;

    /// <summary>The time, in seconds from the clip start, at a pixel.</summary>
    public double Seconds(double x) => x / PixelsPerSecond;

    /// <summary>The top of the row of a pitch in pixels, the highest pitch at 0.</summary>
    public double RowTop(int pitch) => (Pitches - 1 - pitch) * RowHeight;

    /// <summary>The pitch whose row holds a pixel, kept between 0 and 127.</summary>
    public int PitchAt(double y) => Math.Clamp(Pitches - 1 - (int)Math.Floor(y / RowHeight), 0, Pitches - 1);
}
