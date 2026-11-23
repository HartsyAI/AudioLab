namespace AudioLab.Audio.Tests.Support;

/// <summary>White noise from a fixed generator, so a test with noise gives the same result every run.</summary>
public static class Noise
{
    public static PcmAudio Create(int rate, double seconds, double amplitude = 0.5, uint seed = 1)
    {
        float[] samples = new float[(int)Math.Round(seconds * rate)];
        uint state = seed;
        for (int i = 0; i < samples.Length; i++)
        {
            state = unchecked((state * 1664525) + 1013904223);
            samples[i] = (float)(amplitude * (((state >> 8) / 8388608.0) - 1));
        }
        return new PcmAudio(rate, [samples, samples]);
    }
}
