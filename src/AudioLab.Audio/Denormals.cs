namespace AudioLab.Audio;

/// <summary>Sets numbers that are too small to matter to zero, since arithmetic on denormal numbers is far slower than on any other (D60).</summary>
/// <remarks>
/// A level under <see cref="SampleFloor"/> (-360 dBFS) is silence. The floors are far above the denormal range and high enough that the
/// square of a level, a detector's work, is still a normal number. Each flush is one comparison, allocates nothing and does not depend on how
/// the signal is cut into blocks.
/// </remarks>
internal static class Denormals
{
    /// <summary>The level of a sample under which it is silence.</summary>
    public const float SampleFloor = 1e-18f;

    /// <summary>The size of a filter or envelope state under which it is zero.</summary>
    public const double StateFloor = 1e-30;

    /// <summary>The value, or zero when it is under <see cref="SampleFloor"/>.</summary>
    public static float Flush(float value) => Math.Abs(value) < SampleFloor ? 0 : value;

    /// <summary>The value, or zero when it is under <see cref="StateFloor"/>.</summary>
    public static double Flush(double value) => Math.Abs(value) < StateFloor ? 0 : value;

    /// <summary>Flushes every sample of a block.</summary>
    public static void Flush(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Flush(samples[i]);
        }
    }
}
