using AudioLab.Audio.Tests.Support;

namespace AudioLab.Audio.Tests;

public sealed class WavWriterTests
{
    private const int Rate = 44100;

    private static RenderedAudio Audio(float[] left, float[]? right = null) => new(new AudioFormat(Rate, 2), left, right ?? left);

    private static byte[] Write(RenderedAudio audio, WavOptions options, out long clipped)
    {
        using MemoryStream stream = new();
        clipped = WavExporter.Write(audio, stream, options).ClippedSamples;
        return stream.ToArray();
    }

    private static WavOptions Options(WavFormat format, DitherKind dither = DitherKind.None) => new() { Format = format, Dither = dither };

    [Fact]
    public void ASixteenBitFileHasTheHeaderOfPlainPcm()
    {
        WavFile file = new(Write(Audio(new float[10]), Options(WavFormat.Pcm16), out _));

        Assert.Equal(1, file.FormatTag);
        Assert.Equal(2, file.Channels);
        Assert.Equal(Rate, file.SampleRate);
        Assert.Equal(Rate * 4, file.ByteRate);
        Assert.Equal(4, file.BlockAlign);
        Assert.Equal(16, file.Bits);
        Assert.Equal(16, file.FormatSize);
        Assert.Equal(10, file.Frames);
        Assert.Null(file.FactFrames);
    }

    [Fact]
    public void ATwentyFourBitFileHasThreeBytesASample()
    {
        WavFile file = new(Write(Audio(new float[7]), Options(WavFormat.Pcm24), out _));

        Assert.Equal(1, file.FormatTag);
        Assert.Equal(6, file.BlockAlign);
        Assert.Equal(24, file.Bits);
        Assert.Equal(7, file.Frames);
    }

    [Fact]
    public void AFloatFileHasTheHeaderThatAnythingButPcmNeedsAndKeepsEverySampleAsItWas()
    {
        float[] samples = [0.1f, -0.25f, 1.5f, -3f, 0, float.Epsilon];

        WavFile file = new(Write(Audio(samples, [.. samples.Reverse()]), Options(WavFormat.Float32), out long clipped));

        Assert.Equal(3, file.FormatTag);
        Assert.Equal(18, file.FormatSize);
        Assert.Equal(6, file.FactFrames);
        Assert.Equal(32, file.Bits);
        Assert.Equal(0, clipped);
        for (int i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i], file.Float(i, 0));
            Assert.Equal(samples[^(i + 1)], file.Float(i, 1));
        }
    }

    [Fact]
    public void IntegerSamplesFillTheRangeAndRoundToTheNearestStep()
    {
        float[] samples = [0, 0.5f, -0.5f, 1, -1, 0.99999f, 1f / 65536];

        WavFile sixteen = new(Write(Audio(samples), Options(WavFormat.Pcm16), out _));
        WavFile twentyFour = new(Write(Audio(samples), Options(WavFormat.Pcm24), out _));

        Assert.Equal([0L, 16384, -16384, 32767, -32768, 32767, 1], Enumerable.Range(0, 7).Select(i => sixteen.Raw(i, 0)));
        Assert.Equal([0L, 4194304, -4194304, 8388607, -8388608, 8388524, 128], Enumerable.Range(0, 7).Select(i => twentyFour.Raw(i, 0)));
    }

    [Fact]
    public void ASampleBeyondFullScaleIsClampedAndCounted()
    {
        float[] samples = [1.5f, -2f, 0.5f, float.NaN, float.PositiveInfinity];

        WavFile file = new(Write(Audio(samples), Options(WavFormat.Pcm16), out long clipped));

        Assert.Equal([32767L, -32768, 16384, 0, 0], Enumerable.Range(0, 5).Select(i => file.Raw(i, 0)));
        // Two samples, each in both channels.
        Assert.Equal(4, clipped);
    }

    [Fact]
    public void ASampleThatIsNotANumberIsCountedWhateverTheFormat()
    {
        float[] samples = [0.1f, float.NaN, float.NegativeInfinity];

        using MemoryStream integer = new();
        using MemoryStream floating = new();
        WavResult pcm = WavExporter.Write(Audio(samples), integer, Options(WavFormat.Pcm24));
        WavResult real = WavExporter.Write(Audio(samples), floating, Options(WavFormat.Float32));

        // Two samples in each of two channels.
        Assert.Equal(4, pcm.NonFiniteSamples);
        Assert.Equal(4, real.NonFiniteSamples);
        Assert.True(pcm.HasProblems);
        Assert.False(WavExporter.Write(Audio([0.1f]), new MemoryStream(), Options(WavFormat.Pcm16)).HasProblems);
    }

    [Fact]
    public void WithoutDitherARoundingErrorIsAtMostHalfAStepAndTriangularDitherAddsItsOwnNoise()
    {
        float[] samples = [.. Enumerable.Range(0, 20000).Select(i => (float)(0.3 * Math.Sin(i * 0.01)))];

        double plain = ErrorRms(samples, DitherKind.None);
        double triangular = ErrorRms(samples, DitherKind.Triangular);
        double rectangular = ErrorRms(samples, DitherKind.Rectangular);

        // In steps of the 16-bit scale: a uniform error has a variance of 1/12, rectangular dither adds 1/12 and triangular adds 1/6.
        Assert.Equal(Math.Sqrt(1.0 / 12), plain, 1);
        Assert.Equal(Math.Sqrt(1.0 / 6), rectangular, 1);
        Assert.Equal(Math.Sqrt(1.0 / 4), triangular, 1);
    }

    [Fact]
    public void DitherKeepsATonePastTheStepThatPlainRoundingLoses()
    {
        // A tone of a third of a step: plain rounding gives silence, dither keeps it under the noise.
        float amplitude = 1f / 3 / 32768;
        float[] samples = [.. Enumerable.Range(0, 44100).Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * 1000 * i / Rate)))];

        float[] plain = Decode(samples, DitherKind.None);
        float[] dithered = Decode(samples, DitherKind.Triangular);

        Assert.Equal(0, Signals.Peak(plain));
        Assert.InRange(Signals.Level(dithered, 1000, Rate) / amplitude, 0.9, 1.1);
    }

    [Fact]
    public void TheSameSeedGivesTheSameFileAndAnotherSeedGivesAnother()
    {
        float[] samples = [.. Enumerable.Range(0, 1000).Select(i => (float)(0.2 * Math.Sin(i * 0.05)))];
        WavOptions one = Options(WavFormat.Pcm16, DitherKind.Triangular);

        byte[] first = Write(Audio(samples), one, out _);
        byte[] again = Write(Audio(samples), one, out _);
        byte[] other = Write(Audio(samples), one with { Seed = 2 }, out _);

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void TheTwoChannelsAreDitheredWithDifferentNoise()
    {
        WavFile file = new(Write(Audio(new float[2000]), Options(WavFormat.Pcm16, DitherKind.Triangular), out _));

        int equal = Enumerable.Range(0, 2000).Count(i => file.Raw(i, 0) == file.Raw(i, 1));

        Assert.True(equal < 1500);
    }

    [Fact]
    public void ADitherIsIgnoredByAFloatFile()
    {
        float[] samples = [0.1f, 0.2f];

        Assert.Equal(
            Write(Audio(samples), Options(WavFormat.Float32), out _),
            Write(Audio(samples), Options(WavFormat.Float32, DitherKind.Triangular), out _));
    }

    [Fact]
    public void WritingMoreThanTheHeaderSaidOrLessIsAnError()
    {
        using MemoryStream stream = new();
        WavStreamWriter writer = new(stream, Rate, 4, new WavOptions());

        Assert.Throws<InvalidOperationException>(() => writer.Write(new float[3], new float[3]));
        writer.WriteHeader();
        Assert.Throws<InvalidOperationException>(() => writer.Write(new float[5], new float[5]));
        writer.Write(new float[3], new float[3]);
        Assert.Throws<InvalidOperationException>(writer.Complete);
    }

    [Fact]
    public void AFileThatWouldPassFourGibibytesIsRefusedBeforeAnythingIsWritten()
    {
        using MemoryStream stream = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => new WavStreamWriter(stream, Rate, 1L << 30, new WavOptions { Format = WavFormat.Pcm24 }));
        Assert.Equal(0, stream.Length);
    }

    private static float[] Decode(float[] samples, DitherKind dither)
    {
        WavFile file = new(Write(Audio(samples), Options(WavFormat.Pcm16, dither), out _));

        return [.. Enumerable.Range(0, samples.Length).Select(i => file.Raw(i, 0) / 32768f)];
    }

    // The root mean square of the difference between the file and the original, in steps of the 16-bit scale.
    private static double ErrorRms(float[] samples, DitherKind dither)
    {
        WavFile file = new(Write(Audio(samples), Options(WavFormat.Pcm16, dither), out _));
        double sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double error = file.Raw(i, 0) - (samples[i] * 32768.0);
            sum += error * error;
        }
        return Math.Sqrt(sum / samples.Length);
    }

    [Theory]
    [InlineData(WavFormat.Pcm16, 1073741814L, 1073741815L)]
    [InlineData(WavFormat.Pcm24, 715827876L, 715827877L)]
    public void TheRiffLimitIsExactlyAtTheLastFrameThatFits(WavFormat format, long fits, long tooMany)
    {
        WavOptions options = new() { Format = format };

        Assert.True(WavStreamWriter.SizeOf(fits, options) - 8 <= uint.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => WavStreamWriter.SizeOf(tooMany, options));
        using MemoryStream stream = new();
        _ = new WavStreamWriter(stream, Rate, fits, options);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WavStreamWriter(stream, Rate, tooMany, options));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task TheAsynchronousPathWritesTheSameBytesToAStreamThatRefusesSynchronousWrites()
    {
        float[] samples = [.. Enumerable.Range(0, 9000).Select(i => (float)(0.4 * Math.Sin(i * 0.02)))];
        RenderedAudio audio = Audio(samples);
        WavOptions options = new() { Format = WavFormat.Pcm16, Dither = DitherKind.Triangular };
        using MemoryStream expected = new();
        WavExporter.Write(audio, expected, options);
        AsyncOnlyStream asyncOnly = new();

        WavStreamWriter writer = new(asyncOnly, Rate, audio.Frames, options);
        await writer.WriteHeaderAsync();
        await writer.WriteAsync(audio.Left, audio.Right);
        await writer.CompleteAsync();

        Assert.Equal(expected.ToArray(), asyncOnly.Bytes);
    }
}
