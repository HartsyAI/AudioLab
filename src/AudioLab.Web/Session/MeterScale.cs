namespace AudioLab.Web.Session;

/// <summary>Maps a peak level to how high a meter bar is drawn: decibels on a straight scale, so the colour steps fall at -12 and -3 dBFS.</summary>
public static class MeterScale
{
    // The bar changes colour at 68 % and 90 % of its height (Meter.razor.css), which a straight dB scale puts at -12 and -3 dBFS
    // when the bar runs from -40 dBFS at the bottom to 1 dBFS at the top.
    private const double FloorDecibels = -40;
    private const double SpanDecibels = 41;

    /// <summary>The height from 0 to 1 of a linear level where 1 is full scale; silence and anything under -40 dBFS is 0.</summary>
    public static double Height(double level)
    {
        if (!(level > 0))
        {
            return 0;
        }
        double decibels = 20 * Math.Log10(level);
        return Math.Clamp((decibels - FloorDecibels) / SpanDecibels, 0, 1);
    }
}
