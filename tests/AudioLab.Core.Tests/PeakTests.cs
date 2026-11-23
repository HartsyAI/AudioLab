using System.Buffers.Binary;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class PeakTests
{
    // A reference that shares no code with the builder: the lowest and highest sample of each run, straight from the samples.
    private static short[] Reference(short[] interleaved, int channels, int framesPerPeak)
    {
        long frames = interleaved.Length / channels;
        int peaks = (int)((frames + framesPerPeak - 1) / framesPerPeak);
        short[] result = new short[peaks * channels * 2];
        for (int peak = 0; peak < peaks; peak++)
        {
            long from = (long)peak * framesPerPeak;
            long to = Math.Min(from + framesPerPeak, frames);
            for (int channel = 0; channel < channels; channel++)
            {
                short min = short.MaxValue;
                short max = short.MinValue;
                for (long frame = from; frame < to; frame++)
                {
                    short sample = interleaved[(frame * channels) + channel];
                    min = Math.Min(min, sample);
                    max = Math.Max(max, sample);
                }
                result[(peak * channels * 2) + (channel * 2)] = min;
                result[(peak * channels * 2) + (channel * 2) + 1] = max;
            }
        }
        return result;
    }

    private static short[] Signal(int frames, int channels, int seed)
    {
        Random random = new(seed);
        short[] samples = new short[frames * channels];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)random.Next(short.MinValue, short.MaxValue + 1);
        }
        return samples;
    }

    private static float[] ToFloats(short[] samples) => [.. samples.Select(sample => sample / 32768f)];

    private static PeakData Build(short[] samples, int channels, int chunkFrames = int.MaxValue)
    {
        PeakBuilder builder = new(channels, 48000);
        float[] floats = ToFloats(samples);
        int chunk = (int)Math.Min((long)chunkFrames * channels, floats.Length);
        for (int offset = 0; offset < floats.Length; offset += chunk)
        {
            builder.AddFrames(floats.AsSpan(offset, Math.Min(chunk, floats.Length - offset)));
        }
        return builder.Build();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(63, 2)]
    [InlineData(64, 2)]
    [InlineData(65, 1)]
    [InlineData(255, 2)]
    [InlineData(256, 6)]
    [InlineData(257, 2)]
    [InlineData(16384, 2)]
    [InlineData(16385, 1)]
    [InlineData(100_003, 2)]
    public void EveryLevelIsTheLowestAndHighestSampleOfEachRunOfFrames(int frames, int channels)
    {
        short[] samples = Signal(frames, channels, frames);

        PeakData peaks = Build(samples, channels);

        Assert.Equal(frames, peaks.Frames);
        Assert.Equal(channels, peaks.Channels);
        Assert.Equal([64, 256, 1024, 4096, 16384], peaks.Levels.Select(level => level.FramesPerPeak));
        foreach (PeakLevel level in peaks.Levels)
        {
            Assert.Equal(PeakFile.PeakCount(frames, level.FramesPerPeak), level.PeakCount(channels));
            Assert.True(Reference(samples, channels, level.FramesPerPeak).AsSpan().SequenceEqual(level.Values), $"level {level.FramesPerPeak}");
        }
    }

    [Fact]
    public void AKnownSignalGivesPeaksYouCanReadOffByHand()
    {
        // Left climbs 0, 1, 2 ... and right holds -300 except one spike; 200 frames: buckets of 64, 64, 64 and 8.
        short[] samples = new short[400];
        for (int frame = 0; frame < 200; frame++)
        {
            samples[frame * 2] = (short)frame;
            samples[(frame * 2) + 1] = frame == 130 ? (short)12000 : (short)-300;
        }

        PeakData peaks = Build(samples, 2);

        short[] finest = peaks.Levels[0].Values.ToArray();
        Assert.Equal([0, 63, -300, -300, 64, 127, -300, -300, 128, 191, -300, 12000, 192, 199, -300, -300], finest);
        Assert.Equal([0, 199, -300, 12000], peaks.Levels[1].Values.ToArray());
        Assert.Equal([0, 199, -300, 12000], peaks.Levels[4].Values.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(1000)]
    public void TheSizeOfTheBlocksTheSamplesArriveInChangesNothing(int chunkFrames)
    {
        short[] samples = Signal(5000, 2, 7);
        byte[] whole = PeakFile.Write(Build(samples, 2));
        Assert.Equal(whole, PeakFile.Write(Build(samples, 2, chunkFrames)));
    }

    [Theory]
    [InlineData(1.0f, 32767)]
    [InlineData(-1.0f, -32768)]
    [InlineData(2.5f, 32767)]
    [InlineData(-9f, -32768)]
    [InlineData(0.5f, 16384)]
    [InlineData(0f, 0)]
    [InlineData(0.5f / 32768f, 0)]
    [InlineData(1.5f / 32768f, 2)]
    [InlineData(2.5f / 32768f, 2)]
    public void ASampleIsTimes32768RoundedToEvenAndClamped(float sample, int expected)
    {
        Assert.Equal(expected, PeakBuilder.Quantize(sample));
    }

    [Fact]
    public void ABuilderRefusesWhatIsNotAFileOrNotWholeFrames()
    {
        Assert.Throws<InvalidOperationException>(() => new PeakBuilder(2, 48000).Build());
        Assert.Throws<ArgumentException>(() => new PeakBuilder(2, 48000).AddFrames(new float[3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeakBuilder(0, 48000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeakBuilder(9, 48000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeakBuilder(2, 100));
    }

    [Fact]
    public void AFileRoundTripsAndHasTheLengthItsHeaderSays()
    {
        PeakData peaks = Build(Signal(3000, 2, 3), 2);

        byte[] bytes = PeakFile.Write(peaks);
        PeakData read = PeakFile.Read(bytes);

        Assert.Equal(PeakFile.ReadHeader(bytes).TotalBytes, bytes.Length);
        Assert.Equal((peaks.Channels, peaks.SampleRate, peaks.Frames), (read.Channels, read.SampleRate, read.Frames));
        for (int level = 0; level < peaks.Levels.Count; level++)
        {
            Assert.Equal(peaks.Levels[level].FramesPerPeak, read.Levels[level].FramesPerPeak);
            Assert.True(peaks.Levels[level].Values.SequenceEqual(read.Levels[level].Values));
        }
        Assert.Equal("ALPK", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void AFileThatIsNotExactlyWhatItsHeaderSaysIsRefused()
    {
        byte[] good = PeakFile.Write(Build(Signal(3000, 2, 3), 2));
        Assert.Throws<InvalidDataException>(() => PeakFile.Read(good[..^1]));
        Assert.Throws<InvalidDataException>(() => PeakFile.Read([.. good, 0]));
        Assert.Throws<InvalidDataException>(() => PeakFile.Read(good[..10]));
        Assert.Throws<InvalidDataException>(() => PeakFile.Read([]));

        void Refused(Action<byte[]> damage)
        {
            byte[] bad = [.. good];
            damage(bad);
            Assert.Throws<InvalidDataException>(() => PeakFile.Read(bad));
        }

        Refused(bad => bad[0] = (byte)'X');
        Refused(bad => BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(4), 2));
        Refused(bad => BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(6), 0));
        Refused(bad => BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(6), 9));
        Refused(bad => BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(8), 100));
        Refused(bad => BinaryPrimitives.WriteUInt64LittleEndian(bad.AsSpan(12), 0));
        Refused(bad => BinaryPrimitives.WriteUInt64LittleEndian(bad.AsSpan(12), 4000));
        Refused(bad => BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(20), 0));
        Refused(bad => BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(20), 17));
        Refused(bad => BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(22), 1));
        Refused(bad => BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(PeakFile.HeaderBytes), 0));
        Refused(bad => BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(PeakFile.HeaderBytes + PeakFile.LevelEntryBytes), 64));
        Refused(bad => BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(PeakFile.HeaderBytes + 4), 1));
    }
}
