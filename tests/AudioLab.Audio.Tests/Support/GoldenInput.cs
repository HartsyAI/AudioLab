namespace AudioLab.Audio.Tests.Support;

/// <summary>The one signal every golden render starts from: two tones, a burst of noise and a click.</summary>
/// <remarks>It is half a second at 8 kHz unless another length and rate are asked for.</remarks>
public static class GoldenInput
{
    public const int Rate = 8000;

    public static PcmAudio Create(int rate = Rate, double seconds = 0.5)
    {
        float[] samples = new float[(int)(rate * seconds)];
        int burstStart = samples.Length * 3 / 10;
        int burstEnd = samples.Length * 34 / 100;
        int click = samples.Length * 6 / 10;
        uint state = 12345;
        for (int i = 0; i < samples.Length; i++)
        {
            double time = (double)i / rate;
            double value = (0.35 * Math.Sin(2 * Math.PI * 220 * time)) + (0.15 * Math.Sin(2 * Math.PI * 1100 * time));
            state = unchecked((state * 1664525) + 1013904223);
            if (i >= burstStart && i < burstEnd)
            {
                value += 0.5 * ((state >> 8) / 8388608.0 - 1);
            }
            samples[i] = (float)value;
        }
        samples[click] += 0.9f;
        return new PcmAudio(rate, [samples]);
    }
}
