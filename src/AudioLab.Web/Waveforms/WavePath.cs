using System.Globalization;
using System.Text;
using AudioLab.Core.Model;

namespace AudioLab.Web.Waveforms;

/// <summary>Draws the window of a media file that a clip plays as one closed path from a level of its peaks.</summary>
public static class WavePath
{
    /// <summary>The most columns a path has, so a very long clip does not make a very long path.</summary>
    public const int MaxColumns = 2500;

    private const int Box = 1000;

    private const double Middle = 50;

    private const double Reach = 46;

    // A silent column would draw nothing; this is the thinnest line a column keeps, in units of the 100-high box.
    private const double Thinnest = 0.5;

    /// <summary>How many columns a clip of this width is drawn with: one for each pixel, never more than <see cref="MaxColumns"/>.</summary>
    public static int Columns(double widthPixels) => Math.Clamp((int)Math.Round(widthPixels), 2, MaxColumns);

    /// <summary>
    /// The path of the lowest and highest sample of every column of the window, over all channels: the top edge from left to right, then
    /// the bottom edge back, in a 1000 by 100 box with silence on the middle line.
    /// </summary>
    /// <param name="level">The level of peaks to draw from.</param>
    /// <param name="channels">Channels of the media file.</param>
    /// <param name="sampleRate">Frames a second of the media file.</param>
    /// <param name="offsetSeconds">Where in the file the window starts.</param>
    /// <param name="visibleSeconds">How long the window is.</param>
    /// <param name="columns">How many columns to draw, from <see cref="Columns"/>.</param>
    public static string Build(PeakLevel level, int channels, int sampleRate, double offsetSeconds, double visibleSeconds, int columns)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 2);
        int peaks = level.PeakCount(channels);
        ReadOnlySpan<short> values = level.Values;
        double firstPeak = offsetSeconds * sampleRate / level.FramesPerPeak;
        double peaksPerColumn = visibleSeconds * sampleRate / level.FramesPerPeak / columns;
        string[] bottom = new string[columns];
        StringBuilder path = new("M0,");
        path.Append(Middle.ToString("0", CultureInfo.InvariantCulture));
        for (int column = 0; column < columns; column++)
        {
            int from = Math.Clamp((int)Math.Floor(firstPeak + (column * peaksPerColumn)), 0, peaks - 1);
            int to = Math.Clamp((int)Math.Ceiling(firstPeak + ((column + 1) * peaksPerColumn)), from + 1, peaks);
            int low = short.MaxValue;
            int high = short.MinValue;
            for (int peak = from; peak < to; peak++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    int at = ((peak * channels) + channel) * 2;
                    low = Math.Min(low, values[at]);
                    high = Math.Max(high, values[at + 1]);
                }
            }
            double top = Math.Min(Middle - (high / 32768.0 * Reach), Middle - Thinnest);
            double under = Math.Max(Middle - (low / 32768.0 * Reach), Middle + Thinnest);
            string x = (column / (double)(columns - 1) * Box).ToString("0.#", CultureInfo.InvariantCulture);
            path.Append(" L").Append(x).Append(',').Append(top.ToString("0.#", CultureInfo.InvariantCulture));
            bottom[column] = string.Concat(" L", x, ",", under.ToString("0.#", CultureInfo.InvariantCulture));
        }
        for (int column = columns - 1; column >= 0; column--)
        {
            path.Append(bottom[column]);
        }
        return path.Append(" Z").ToString();
    }
}
