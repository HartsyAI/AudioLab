namespace AudioLab.Audio.Tests;

public sealed class EngineTests
{
    private const int Rate = 48000;

    [Fact]
    public void RenderingTheSameSessionTwiceGivesTheSameSamples()
    {
        float[] first = Render(StressMix.Build(Rate, 128, []), 8, 512);
        float[] second = Render(StressMix.Build(Rate, 128, []), 8, 512);

        Assert.Equal(first, second);
        Assert.All(first, sample => Assert.True(float.IsFinite(sample) && Math.Abs(sample) <= 1f));
        Assert.Contains(first, sample => Math.Abs(sample) > 0.001f);
    }

    [Fact]
    public void RenderingAllocatesNothingOnceItIsWarm()
    {
        short[] samples = new short[Rate * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(i % 2000);
        }
        AudioEngine engine = StressMix.Build(Rate, 128, [new SampleClip(samples, 44100)]);
        ChunkRenderer renderer = new(engine, 512);
        renderer.Render();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            renderer.Render();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ALowPassKeepsTheBassAndCutsTheTreble()
    {
        float low = Gain(frequency: 100, cutoff: 1000);
        float high = Gain(frequency: 8000, cutoff: 1000);

        Assert.InRange(low, 0.95f, 1.05f);
        Assert.True(high < 0.05f, $"gain at 8 kHz was {high}");
    }

    [Fact]
    public void AClipThatIsNotSixteenBitPcmIsRefused()
    {
        byte[] file = new byte[44];
        "RIFF"u8.CopyTo(file);
        "WAVE"u8.CopyTo(file.AsSpan(8));
        "fmt "u8.CopyTo(file.AsSpan(12));
        file[16] = 16;
        file[20] = 3;
        file[22] = 2;
        file[24] = 0x80;
        file[25] = 0xBB;
        file[34] = 32;
        "data"u8.CopyTo(file.AsSpan(36));

        Assert.Throws<FormatException>(() => WavReader.Read(file));
    }

    private static float[] Render(AudioEngine engine, int chunks, int frames)
    {
        float[] output = new float[chunks * frames * 2];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int offset = chunk * frames * 2;
            engine.Render(output.AsSpan(offset, frames), output.AsSpan(offset + frames, frames));
        }
        return output;
    }

    private static float Gain(float frequency, float cutoff)
    {
        BiquadFilter filter = new();
        filter.SetLowPass(cutoff, 0.7071f, Rate);
        float[] signal = new float[Rate];
        for (int i = 0; i < signal.Length; i++)
        {
            signal[i] = MathF.Sin(2 * MathF.PI * frequency * i / Rate);
        }
        filter.Process(signal);
        float peak = 0;
        for (int i = Rate / 2; i < signal.Length; i++)
        {
            peak = MathF.Max(peak, MathF.Abs(signal[i]));
        }
        return peak;
    }
}
