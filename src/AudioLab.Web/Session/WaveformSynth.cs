using System.Globalization;
using System.Text;

namespace AudioLab.Web.Session;

/// <summary>Synthetic peaks and notes, the same generators as the design mock so the first six tracks match it.</summary>
public static class WaveformSynth
{
    /// <summary>Peak amplitudes (0 to 1) for a clip of the given kind.</summary>
    public static float[] Peaks(int seed, int count, TrackKind kind)
    {
        Lcg random = new((uint)((seed * 7919) + 13));
        float[] peaks = new float[count];
        double envelope = 0.5;
        double level = 0;
        for (int i = 0; i < count; i++)
        {
            double value;
            switch (kind)
            {
                case TrackKind.Drums:
                    {
                        int p = i % 8;
                        double hit = p == 0 ? 1 : (p == 4 ? 0.8 : (p is 2 or 6 ? 0.34 : 0));
                        level = Math.Max(hit * (0.82 + (0.18 * random.Next())), level * 0.58);
                        value = level + (0.05 * random.Next());
                        break;
                    }
                case TrackKind.Bass:
                    {
                        int p = i % 10;
                        level = p is 0 or 6 ? 0.78 + (0.16 * random.Next()) : level * 0.93;
                        value = level * (0.9 + (0.1 * random.Next()));
                        break;
                    }
                case TrackKind.Guitar:
                    {
                        int p = i % 8;
                        double strum = p == 0 ? 0.92 : (p == 3 ? 0.6 : (p == 6 ? 0.76 : 0));
                        level = strum > 0 ? strum * (0.8 + (0.2 * random.Next())) : level * 0.8;
                        value = level * (0.72 + (0.28 * Math.Abs(Math.Sin((i / 19.0) + seed)))) * (0.85 + (0.15 * random.Next()));
                        break;
                    }
                default:
                    {
                        int phrase = i % 46;
                        envelope += (random.Next() - 0.5) * 0.3;
                        envelope = Math.Max(0.25, Math.Min(0.92, envelope));
                        double gate = phrase > 37 ? 0.06 : (Math.Sin(phrase / 37.0 * Math.PI) * 0.75) + 0.25;
                        value = envelope * gate * (0.6 + (0.4 * random.Next()));
                        break;
                    }
            }
            peaks[i] = (float)Math.Max(0.03, Math.Min(1, value));
        }
        return peaks;
    }

    /// <summary>Chord-like MIDI notes for a clip of the given length in bars.</summary>
    public static NoteSpan[] Notes(int seed, int bars)
    {
        Lcg random = new((uint)((seed * 104729) + 7));
        int steps = bars * 2;
        int[][] rows = [[22, 44, 66], [30, 52, 74], [16, 38, 60], [26, 48, 70]];
        List<NoteSpan> notes = new(steps * 3);
        for (int i = 0; i < steps; i++)
        {
            int[] chord = rows[(i / 2) % rows.Length];
            double hold = random.Next() > 0.35 ? 1 : 0.6;
            for (int k = 0; k < 3; k++)
            {
                notes.Add(new NoteSpan((float)((i / (double)steps * 100) + 0.4), chord[k], (float)((100.0 / steps * hold) - 0.8)));
            }
        }
        return [.. notes];
    }

    /// <summary>The waveform as an SVG path in a 1000 by 100 box, mirrored about the centre line.</summary>
    public static string PathData(float[] peaks)
    {
        StringBuilder top = new("M0,50");
        List<string> bottom = new(peaks.Length);
        int count = peaks.Length;
        for (int i = 0; i < count; i++)
        {
            string x = (i / (double)(count - 1) * 1000).ToString("0.0", CultureInfo.InvariantCulture);
            top.Append(" L").Append(x).Append(',').Append((50 - (peaks[i] * 44)).ToString("0.0", CultureInfo.InvariantCulture));
            bottom.Add(string.Concat(" L", x, ",", (50 + (peaks[i] * 44)).ToString("0.0", CultureInfo.InvariantCulture)));
        }
        for (int i = count - 1; i >= 0; i--)
        {
            top.Append(bottom[i]);
        }
        return top.Append(" Z").ToString();
    }
}
