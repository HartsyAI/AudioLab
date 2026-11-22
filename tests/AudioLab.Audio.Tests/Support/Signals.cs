namespace AudioLab.Audio.Tests.Support;

/// <summary>Test signals and measurements.</summary>
public static class Signals
{
    public static PcmAudio Constant(float value, double seconds, int rate = 48000, int channels = 2)
    {
        float[][] data = new float[channels][];
        for (int channel = 0; channel < channels; channel++)
        {
            data[channel] = Enumerable.Repeat(value, (int)Math.Round(seconds * rate)).ToArray();
        }
        return new PcmAudio(rate, data);
    }

    public static PcmAudio Sine(double hertz, double seconds, int rate = 48000, double amplitude = 0.5, int channels = 2)
    {
        float[] samples = new float[(int)Math.Round(seconds * rate)];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * hertz * i / rate));
        }
        return new PcmAudio(rate, [.. Enumerable.Repeat(samples, channels)]);
    }

    public static PcmAudio Stereo(float[] left, float[] right, int rate = 48000) => new(rate, [left, right]);

    public static double Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        foreach (float sample in samples)
        {
            sum += (double)sample * sample;
        }
        return samples.Length == 0 ? 0 : Math.Sqrt(sum / samples.Length);
    }

    public static double Peak(ReadOnlySpan<float> samples)
    {
        double peak = 0;
        foreach (float sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }
        return peak;
    }

    /// <summary>The amplitude of one frequency in the samples, by the Goertzel algorithm.</summary>
    public static double Level(ReadOnlySpan<float> samples, double hertz, int rate = 48000)
    {
        double coefficient = 2 * Math.Cos(2 * Math.PI * hertz / rate);
        double previous = 0;
        double before = 0;
        foreach (float sample in samples)
        {
            double current = sample + (coefficient * previous) - before;
            before = previous;
            previous = current;
        }
        double power = (previous * previous) + (before * before) - (coefficient * previous * before);
        return 2 * Math.Sqrt(Math.Max(0, power)) / samples.Length;
    }
}
