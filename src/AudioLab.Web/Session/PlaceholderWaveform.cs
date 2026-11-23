using System.Globalization;
using System.Text;

namespace AudioLab.Web.Session;

/// <summary>PLACEHOLDER peaks: no waveform exists until the media store does (phase 6); this is a stand-in, the same for the same clip.</summary>
/// <remarks>Nothing here reads audio. It is not a measurement of the clip, and it goes when real peaks arrive.</remarks>
public static class PlaceholderWaveform
{
    private const int Box = 1000;

    private const int Half = 50;

    private const double Reach = 44;

    /// <summary>The most points a path has, so a very long clip does not make a very long path.</summary>
    public const int MaxPoints = 4000;

    /// <summary>Stand-in peak amplitudes from 0 to 1, deterministic in the seed text.</summary>
    public static float[] Peaks(string seed, int count)
    {
        ArgumentNullException.ThrowIfNull(seed);
        int points = Math.Clamp(count, 2, MaxPoints);
        uint state = Hash(seed);
        float[] peaks = new float[points];
        double envelope = 0.5;
        for (int i = 0; i < points; i++)
        {
            state = unchecked((state * 1664525) + 1013904223);
            double noise = state / 4294967296.0;
            int phrase = i % 46;
            envelope = Math.Clamp(envelope + ((noise - 0.5) * 0.3), 0.25, 0.92);
            double gate = phrase > 37 ? 0.06 : (Math.Sin(phrase / 37.0 * Math.PI) * 0.75) + 0.25;
            peaks[i] = (float)Math.Clamp(envelope * gate * (0.6 + (0.4 * noise)), 0.03, 1);
        }
        return peaks;
    }

    /// <summary>The path of a mirrored waveform in a 1000 by 100 box, to be stretched over a clip's body.</summary>
    public static string Path(float[] peaks)
    {
        ArgumentNullException.ThrowIfNull(peaks);
        StringBuilder path = new("M0,50");
        string[] bottom = new string[peaks.Length];
        for (int i = 0; i < peaks.Length; i++)
        {
            string x = (i / (double)(peaks.Length - 1) * Box).ToString("0.0", CultureInfo.InvariantCulture);
            path.Append(" L").Append(x).Append(',').Append((Half - (peaks[i] * Reach)).ToString("0.0", CultureInfo.InvariantCulture));
            bottom[i] = string.Concat(" L", x, ",", (Half + (peaks[i] * Reach)).ToString("0.0", CultureInfo.InvariantCulture));
        }
        for (int i = bottom.Length - 1; i >= 0; i--)
        {
            path.Append(bottom[i]);
        }
        return path.Append(" Z").ToString();
    }

    // FNV-1a over the text, so the same clip draws the same stand-in on every machine and every run.
    private static uint Hash(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text)
        {
            hash = unchecked((hash ^ c) * 16777619);
        }
        return hash;
    }
}
