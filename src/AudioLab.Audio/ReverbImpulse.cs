namespace AudioLab.Audio;

/// <summary>The reverb's generated impulse response: noise with a power-law decay, seeded from the settings so a room never changes.</summary>
/// <remarks>The seed and the generator are fixed, so a room always sounds the same; the level is that of the Web Audio convolver.</remarks>
internal static class ReverbImpulse
{
    // The convolver's calibration, which keeps a reverb's loudness near the dry signal's.
    private const double Calibration = 0.00125;
    private const int CalibrationRate = 44100;
    private const double MinPower = 0.000125;

    /// <summary>The impulse of one channel (0 left, 1 right) for a room of <paramref name="size"/> seconds and a decay exponent.</summary>
    public static float[] Create(double size, double decay, int channel, int sampleRate)
    {
        int length = Math.Max(64, (int)Math.Floor((size * sampleRate) + 0.5));
        double seedValue = (Math.Floor((size * 1000) + 0.5) * 7919) + (Math.Floor((decay * 100) + 0.5) * 31) + channel + 1;
        uint state = unchecked((uint)(long)seedValue);
        float[] impulse = new float[length];
        for (int i = 0; i < length; i++)
        {
            double noise = (Next(ref state) * 2) - 1;
            impulse[i] = (float)(noise * Math.Pow(1 - ((double)i / length), decay));
        }
        return impulse;
    }

    /// <summary>The gain that brings a pair of impulses to the convolver's calibrated level.</summary>
    public static double Scale(float[] left, float[] right, int sampleRate)
    {
        double energy = 0;
        foreach (float sample in left)
        {
            energy += (double)sample * sample;
        }
        foreach (float sample in right)
        {
            energy += (double)sample * sample;
        }
        double power = Math.Sqrt(energy / (2.0 * left.Length));
        if (!double.IsFinite(power) || power < MinPower)
        {
            power = MinPower;
        }
        return Calibration / power * CalibrationRate / sampleRate;
    }

    // Mulberry32, a small generator that gives the same numbers on every platform.
    private static double Next(ref uint state)
    {
        unchecked
        {
            state += 0x6D2B79F5;
            uint t = state;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + ((t ^ (t >> 7)) * (t | 61));
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}
