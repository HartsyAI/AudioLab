namespace AudioLab.Audio;

/// <summary>The taps of a half-band low-pass filter: a windowed sinc with its cutoff at a quarter of the rate it runs at.</summary>
/// <remarks>
/// Every second tap except the middle one is zero, so half the work is skipped, and the filter has a linear phase: a delay of half its length.
/// The taps add up to one.
/// </remarks>
internal static class HalfbandCoefficients
{
    /// <summary>The taps of a filter of <paramref name="length"/> taps (odd), the Kaiser window of <paramref name="beta"/> over a sinc.</summary>
    public static float[] Create(int length, double beta)
    {
        if (length < 3 || length % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "A half-band filter has an odd length of at least 3.");
        }
        int half = length / 2;
        double[] taps = new double[length];
        double sum = 0;
        for (int k = -half; k <= half; k++)
        {
            double ideal = k == 0 ? 0.5 : (k % 2 == 0 ? 0 : Math.Sin(Math.PI * k / 2) / (Math.PI * k));
            double ratio = (double)k / half;
            taps[k + half] = ideal * Bessel(beta * Math.Sqrt(1 - (ratio * ratio))) / Bessel(beta);
            sum += taps[k + half];
        }
        return [.. taps.Select(tap => (float)(tap / sum))];
    }

    // The modified Bessel function of the first kind and order zero, by its series.
    private static double Bessel(double x)
    {
        double term = 1;
        double sum = 1;
        for (int k = 1; k < 60; k++)
        {
            term *= (x / (2 * k)) * (x / (2 * k));
            sum += term;
        }
        return sum;
    }
}
