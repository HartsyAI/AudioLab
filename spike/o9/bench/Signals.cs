namespace O9;

/// <summary>Known test signals, so every assertion has a closed-form answer.</summary>
public static class Signals
{
    public static float[] Sine(int rate, double seconds, double freq, double amp = 0.5)
    {
        float[] x = new float[(int)(rate * seconds)];
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = (float)(amp * Math.Sin(2 * Math.PI * freq * i / rate));
        }
        return x;
    }

    /// <summary>Two tones and a little deterministic noise, so a timing run is not a trivially predictable stream.</summary>
    public static float[] Mix(int rate, double seconds)
    {
        Random rng = new(12345);
        float[] x = new float[(int)(rate * seconds)];
        for (int i = 0; i < x.Length; i++)
        {
            double t = (double)i / rate;
            x[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 1000 * t) + 0.2 * Math.Sin(2 * Math.PI * 5300 * t)
                + 0.05 * (rng.NextDouble() * 2 - 1));
        }
        return x;
    }

    public static float MaxAbsDiff(ReadOnlySpan<float> a, ReadOnlySpan<float> b, int from = 0, int to = -1)
    {
        int end = to < 0 ? Math.Min(a.Length, b.Length) : to;
        float max = 0;
        for (int i = from; i < end; i++)
        {
            max = Math.Max(max, Math.Abs(a[i] - b[i]));
        }
        return max;
    }
}
