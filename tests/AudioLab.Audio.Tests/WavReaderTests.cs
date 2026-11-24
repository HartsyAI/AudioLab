using System.Buffers.Binary;
using AudioLab.Audio.Tests.Support;
using AudioLab.Audio.Wav;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class WavReaderTests
{
    private static WavHeader Read(byte[] file) => WavReader.Read(new MemoryStream(file));

    private static void Refused(byte[] file, string reason)
    {
        WavFormatException error = Assert.Throws<WavFormatException>(() => Read(file));
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SampleEncoding.Pcm16, 1, 44100, false)]
    [InlineData(SampleEncoding.Pcm16, 2, 48000, false)]
    [InlineData(SampleEncoding.Pcm24, 2, 96000, false)]
    [InlineData(SampleEncoding.Pcm24, 2, 48000, true)]
    [InlineData(SampleEncoding.Pcm32, 1, 48000, false)]
    [InlineData(SampleEncoding.Float32, 2, 48000, false)]
    [InlineData(SampleEncoding.Float32, 6, 48000, true)]
    public void EveryAcceptedEncodingIsReadWithItsMeasuredShape(SampleEncoding encoding, int channels, int rate, bool extensible)
    {
        WavHeader format = Read(TestWav.Build(encoding, channels, rate, 1000, extensible: extensible));
        Assert.Equal(encoding, format.Encoding);
        Assert.Equal(channels, format.Channels);
        Assert.Equal(rate, format.SampleRate);
        Assert.Equal(1000, format.Frames);
        Assert.Equal(1000.0 / rate, format.Duration);
    }

    [Fact]
    public void ChunksItDoesNotKnowAreSkippedIncludingOddOnesWithAPadByte()
    {
        byte[] file = TestWav.Riff(
            TestWav.Chunk("LIST", [1, 2, 3]),
            TestWav.Format(SampleEncoding.Pcm16, 1, 48000),
            TestWav.Chunk("junk", [9]),
            TestWav.Chunk("data", new byte[200]),
            TestWav.Chunk("bext", [1, 2, 3, 4, 5]));
        Assert.Equal(100, Read(file).Frames);
    }

    [Fact]
    public void ALastChunkOfOddSizeMayLeaveOutItsPadByte()
    {
        byte[] format = TestWav.Format(SampleEncoding.Pcm16, 1, 48000);
        byte[] file = TestWav.Riff(format, TestWav.Chunk("data", new byte[200]), TestWav.Chunk("note", [1, 2, 3]));
        byte[] unpadded = file[..^1];
        BinaryPrimitives.WriteUInt32LittleEndian(unpadded.AsSpan(4), (uint)(unpadded.Length - 8));
        Assert.Equal(100, Read(unpadded).Frames);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(11)]
    public void AFileShorterThanItsHeaderIsRefused(int length)
    {
        Refused(TestWav.Build(SampleEncoding.Pcm16, 1, 48000, 100)[..length], "shorter than a WAVE header");
    }

    [Fact]
    public void ATruncatedFileIsRefusedWhereverTheCutFalls()
    {
        byte[] whole = TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 100);
        foreach (int cut in new[] { 12, 15, 20, 36, 44, 100, whole.Length - 1 })
        {
            Assert.Throws<WavFormatException>(() => Read(whole[..cut]));
        }
    }

    [Fact]
    public void BytesAfterTheEndOfTheRiffDataAreRefused()
    {
        Refused([.. TestWav.Build(SampleEncoding.Pcm16, 1, 48000, 100), 0, 0, 0, 0], "bytes after its RIFF data");
    }

    [Fact]
    public void RiffThatIsNotWaveAndRf64AreRefused()
    {
        byte[] file = TestWav.Build(SampleEncoding.Pcm16, 1, 48000, 100);
        byte[] notWave = [.. file];
        "AVI "u8.CopyTo(notWave.AsSpan(8));
        Refused(notWave, "not a RIFF WAVE");
        byte[] rf64 = [.. file];
        "RF64"u8.CopyTo(rf64);
        Refused(rf64, "RF64");
        Refused("ID3\u0003"u8.ToArray().Concat(new byte[64]).ToArray(), "not a RIFF WAVE");
    }

    [Fact]
    public void ADataChunkWhoseSizeIsUnsetOrPastTheEndIsRefused()
    {
        byte[] unset = TestWav.Build(SampleEncoding.Pcm16, 1, 48000, 100);
        BinaryPrimitives.WriteUInt32LittleEndian(unset.AsSpan(40), uint.MaxValue);
        Refused(unset, "not set");
        byte[] past = TestWav.Build(SampleEncoding.Pcm16, 1, 48000, 100);
        BinaryPrimitives.WriteUInt32LittleEndian(past.AsSpan(40), 100_000);
        Refused(past, "truncated");
    }

    [Fact]
    public void NoAudioDataAtAllIsRefused()
    {
        Refused(TestWav.Riff(TestWav.Format(SampleEncoding.Pcm16, 1, 48000), TestWav.Chunk("data", [])), "no audio data");
        Refused(TestWav.Riff(TestWav.Format(SampleEncoding.Pcm16, 1, 48000)), "no audio data");
        Refused(TestWav.Riff(TestWav.Chunk("data", new byte[10])), "before the fmt");
    }

    [Fact]
    public void DataThatIsNotAWholeNumberOfFramesIsRefused()
    {
        Refused(TestWav.Riff(TestWav.Format(SampleEncoding.Pcm16, 2, 48000), TestWav.Chunk("data", new byte[10])), "whole number of frames");
    }

    [Fact]
    public void DataBeforeFmtAndRepeatedChunksAreRefused()
    {
        byte[] format = TestWav.Format(SampleEncoding.Pcm16, 1, 48000);
        byte[] data = TestWav.Chunk("data", new byte[20]);
        Refused(TestWav.Riff(data, format), "before the fmt");
        Refused(TestWav.Riff(format, format, data), "two fmt");
        Refused(TestWav.Riff(format, data, data), "two data");
    }

    [Theory]
    [InlineData(2, "ADPCM")]
    [InlineData(6, "A-law")]
    [InlineData(7, "mu-law")]
    [InlineData(0x55, "MP3")]
    [InlineData(0xFFFF, "unknown")]
    public void CompressedAndUnknownFormatTagsAreRefusedAndSaySoWithTheAcceptedList(int tag, string label)
    {
        byte[] fmt = TestWav.Chunk("fmt ", TestWav.Fmt((ushort)tag, 1, 48000, 96000, 2, 16));
        WavFormatException error = Assert.Throws<WavFormatException>(() => Read(TestWav.Riff(fmt, TestWav.Chunk("data", new byte[20]))));
        Assert.Contains("16, 24 or 32-bit PCM or 32-bit float", error.Message, StringComparison.Ordinal);
        Assert.NotEmpty(label);
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(1, 12)]
    [InlineData(1, 64)]
    [InlineData(3, 16)]
    [InlineData(3, 64)]
    public void SampleSizesOutsideTheAcceptedFormatsAreRefused(int tag, int bits)
    {
        int blockAlign = bits / 8;
        byte[] fmt = TestWav.Chunk("fmt ", TestWav.Fmt((ushort)tag, 1, 48000, 48000 * blockAlign, blockAlign, bits));
        Assert.Throws<WavFormatException>(() => Read(TestWav.Riff(fmt, TestWav.Chunk("data", new byte[64]))));
    }

    [Theory]
    [InlineData(0, 48000, 96000, 2, 16, "channels")]
    [InlineData(9, 48000, 864000, 18, 16, "channels")]
    [InlineData(1, 7999, 15998, 2, 16, "sample rate")]
    [InlineData(1, 384001, 768002, 2, 16, "sample rate")]
    [InlineData(1, 48000, 96001, 2, 16, "byte rate")]
    [InlineData(1, 48000, 96000, 4, 16, "block size")]
    [InlineData(2, 48000, 96000, 2, 16, "block size")]
    public void AnInconsistentOrOutOfRangeHeaderIsRefused(int channels, int rate, int byteRate, int blockAlign, int bits, string reason)
    {
        byte[] fmt = TestWav.Chunk("fmt ", TestWav.Fmt(1, channels, rate, byteRate, blockAlign, bits));
        Refused(TestWav.Riff(fmt, TestWav.Chunk("data", new byte[64])), reason);
    }

    [Fact]
    public void AnExtensibleFileThatDoesNotFillItsContainerOrHasAnOddSubFormatIsRefused()
    {
        byte[] validBits = TestWav.Build(SampleEncoding.Pcm24, 2, 48000, 10, extensible: true);
        BinaryPrimitives.WriteUInt16LittleEndian(validBits.AsSpan(12 + 8 + 18), 20);
        Refused(validBits, "valid bits");
        byte[] guid = TestWav.Build(SampleEncoding.Pcm24, 2, 48000, 10, extensible: true);
        guid[12 + 8 + 30] = 0x99;
        Refused(guid, "sub-format");
        byte[] shortFmt = TestWav.Chunk("fmt ", [.. TestWav.Fmt(0xFFFE, 2, 48000, 192000, 4, 16), 0, 0]);
        byte[] shortExtension = TestWav.Riff(shortFmt, TestWav.Chunk("data", new byte[40]));
        Refused(shortExtension, "extensible");
    }

    [Fact]
    public void AFmtChunkWithBytesItsFormatDoesNotUseIsRefusedAndAnEmptyExtensionIsNot()
    {
        byte[] data = TestWav.Chunk("data", new byte[40]);
        byte[] plain = TestWav.Fmt(1, 1, 48000, 96000, 2, 16);
        Assert.Equal(20, Read(TestWav.Riff(TestWav.Chunk("fmt ", [.. plain, 0, 0]), data)).Frames);
        Refused(TestWav.Riff(TestWav.Chunk("fmt ", [.. plain, 2, 0]), data), "bytes this format does not use");
        Refused(TestWav.Riff(TestWav.Chunk("fmt ", [.. plain, 0, 0, 0, 0]), data), "bytes this format does not use");
        Refused(TestWav.Riff(TestWav.Chunk("fmt ", [.. plain, 0]), data), "bytes this format does not use");
    }

    [Fact]
    public void AnExtensibleChannelMaskMustNameAsManySpeakersAsTheFileHasChannelsOrNone()
    {
        byte[] file = TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 10, extensible: true);
        byte[] wrong = [.. file];
        BinaryPrimitives.WriteUInt32LittleEndian(wrong.AsSpan(12 + 8 + 20), 0b111);
        Refused(wrong, "channel mask");
        byte[] none = [.. file];
        BinaryPrimitives.WriteUInt32LittleEndian(none.AsSpan(12 + 8 + 20), 0);
        Assert.Equal(10, Read(none).Frames);
    }

    [Fact]
    public void AnOddChunkWithoutItsPadByteInTheMiddleOfTheFileIsNamedAsTheLikelyCause()
    {
        byte[] oddNoPad = [.. "junk"u8, 3, 0, 0, 0, 1, 2, 3];
        byte[] data = TestWav.Chunk("data", [.. Enumerable.Repeat((byte)0x55, 200)]);
        byte[] file = TestWav.Riff(TestWav.Format(SampleEncoding.Pcm16, 1, 48000), oddNoPad, data);
        WavFormatException error = Assert.Throws<WavFormatException>(() => Read(file));
        Assert.Contains("pad byte", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFloatFileWithANanOrAnInfinityIsRefusedAndALegalOneIsNot()
    {
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            byte[] file = TestWav.Build(SampleEncoding.Float32, 2, 48000, 5000, (frame, _) => frame == 4321 ? bad : 0.25);
            using MemoryStream stream = new(file);
            WavHeader format = WavReader.Read(stream);
            Task Scan() => WavReader.RequireFiniteAsync(stream, format, CancellationToken.None);
            WavFormatException error = await Assert.ThrowsAsync<WavFormatException>(Scan);
            Assert.Contains("not a finite number", error.Message, StringComparison.Ordinal);
        }
        byte[] good = TestWav.Build(SampleEncoding.Float32, 2, 48000, 5000, (frame, _) => frame % 2 == 0 ? 1e30 : -1e-40);
        using MemoryStream goodStream = new(good);
        await WavReader.RequireFiniteAsync(goodStream, WavReader.Read(goodStream), CancellationToken.None);
    }
}
