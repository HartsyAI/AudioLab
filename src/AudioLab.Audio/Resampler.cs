namespace AudioLab.Audio;

/// <summary>Converts audio to another sample rate with a windowed-sinc filter, once, when a render is prepared.</summary>
internal static class Resampler
{
    private const int HalfTaps = 16;
    private const int Phases = 256;

    /// <summary>The audio at <paramref name="rate"/>; the same instance when it already is.</summary>
    public static PcmAudio ToRate(PcmAudio source, int rate)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SampleRate == rate)
        {
            return source;
        }
        double ratio = (double)source.SampleRate / rate;
        double cutoff = Math.Min(1, 1 / ratio);
        double extent = HalfTaps / cutoff;
        int reach = (int)Math.Ceiling(extent);
        int taps = 2 * reach;
        float[] table = BuildTable(cutoff, extent, reach, taps);
        int frames = (int)Math.Round(source.Frames / ratio);
        float[][] output = new float[source.ChannelCount][];
        for (int channel = 0; channel < output.Length; channel++)
        {
            output[channel] = Convert(source.Channel(channel), frames, ratio, reach, taps, table);
        }
        return new PcmAudio(rate, output);
    }

    // Row p holds the weights for a fractional position p / Phases past a source sample; each row sums to one.
    private static float[] BuildTable(double cutoff, double extent, int reach, int taps)
    {
        float[] table = new float[(Phases + 1) * taps];
        double[] weights = new double[taps];
        for (int phase = 0; phase <= Phases; phase++)
        {
            double fraction = (double)phase / Phases;
            double sum = 0;
            for (int k = 0; k < taps; k++)
            {
                double distance = (k - reach + 1) - fraction;
                double weight = cutoff * Sinc(cutoff * distance) * Window(distance / extent);
                weights[k] = weight;
                sum += weight;
            }
            for (int k = 0; k < taps; k++)
            {
                table[(phase * taps) + k] = (float)(weights[k] / sum);
            }
        }
        return table;
    }

    private static float[] Convert(ReadOnlySpan<float> source, int frames, double ratio, int reach, int taps, float[] table)
    {
        float[] output = new float[frames];
        for (int n = 0; n < frames; n++)
        {
            double position = n * ratio;
            int index = (int)position;
            double phasePosition = (position - index) * Phases;
            int phase = (int)phasePosition;
            float blend = (float)(phasePosition - phase);
            int first = index - reach + 1;
            float sum = 0;
            for (int k = 0; k < taps; k++)
            {
                int at = first + k;
                if ((uint)at >= (uint)source.Length)
                {
                    continue;
                }
                float weight = table[(phase * taps) + k];
                weight += (table[((phase + 1) * taps) + k] - weight) * blend;
                sum += source[at] * weight;
            }
            output[n] = sum;
        }
        return output;
    }

    private static double Sinc(double x) => Math.Abs(x) < 1e-12 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);

    // Blackman window over -1 to 1.
    private static double Window(double u) =>
        Math.Abs(u) >= 1 ? 0 : 0.42 + (0.5 * Math.Cos(Math.PI * u)) + (0.08 * Math.Cos(2 * Math.PI * u));
}
