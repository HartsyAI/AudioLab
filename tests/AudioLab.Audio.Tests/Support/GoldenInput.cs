namespace AudioLab.Audio.Tests.Support;

/// <summary>The one signal every golden render starts from: two tones, a burst of noise and a click, half a second at 8 kHz.</summary>
public static class GoldenInput
{
    public const int Rate = 8000;

    public static PcmAudio Create()
    {
        float[] samples = new float[Rate / 2];
        uint state = 12345;
        for (int i = 0; i < samples.Length; i++)
        {
            double time = (double)i / Rate;
            double value = (0.35 * Math.Sin(2 * Math.PI * 220 * time)) + (0.15 * Math.Sin(2 * Math.PI * 1100 * time));
            state = unchecked((state * 1664525) + 1013904223);
            if (i >= 1200 && i < 1360)
            {
                value += 0.5 * ((state >> 8) / 8388608.0 - 1);
            }
            samples[i] = (float)value;
        }
        samples[2400] += 0.9f;
        return new PcmAudio(Rate, [samples]);
    }
}
