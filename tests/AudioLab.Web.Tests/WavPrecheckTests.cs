using System.Buffers.Binary;
using System.Text;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of what the start of a file can tell about whether the server will accept it.</summary>
public sealed class WavPrecheckTests
{
    private static byte[] Fmt(ushort tag, int channels, int rate, int bits, int? align = null)
    {
        byte[] chunk = new byte[24];
        Encoding.ASCII.GetBytes("fmt ").CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(8), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(10), (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(12), (uint)rate);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(16), (uint)(rate * channels * (bits / 8)));
        BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(20), (ushort)(align ?? (channels * (bits / 8))));
        BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(22), (ushort)bits);
        return chunk;
    }

    private static byte[] Chunk(string id, uint size, int bodyBytes = 0)
    {
        byte[] chunk = new byte[8 + bodyBytes];
        Encoding.ASCII.GetBytes(id).CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), size);
        return chunk;
    }

    // A file whose head is the given chunks and whose size is length, with its RIFF size right.
    private static (byte[] Head, long Length) File(long length, params byte[][] chunks)
    {
        List<byte> head = [.. "RIFF"u8, 0, 0, 0, 0, .. "WAVE"u8];
        BinaryPrimitives.WriteUInt32LittleEndian(CollectionsMarshalAt(head), (uint)Math.Min(length - 8, uint.MaxValue));
        foreach (byte[] chunk in chunks)
        {
            head.AddRange(chunk);
        }
        return ([.. head], length);
    }

    private static Span<byte> CollectionsMarshalAt(List<byte> head) => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(head)[4..];

    private static (byte[], long) Pcm16(long dataBytes = 4000)
    {
        return File(12 + 24 + 8 + dataBytes, Fmt(1, 2, 44100, 16), Chunk("data", (uint)dataBytes));
    }

    private static string? Problem((byte[] Head, long Length) file) => WavPrecheck.Problem(file.Head, file.Length);

    /// <summary>A plain file of every accepted format passes, as does a file whose fmt chunk is behind a small chunk of another kind.</summary>
    [Theory]
    [InlineData(1, 1, 8000, 16)]
    [InlineData(1, 2, 44100, 16)]
    [InlineData(1, 8, 384000, 24)]
    [InlineData(1, 2, 48000, 32)]
    [InlineData(3, 2, 96000, 32)]
    public void AFileOfEveryAcceptedFormatPasses(int tag, int channels, int rate, int bits)
    {
        int block = channels * (bits / 8);
        (byte[], long) file = File(12 + 24 + 8 + (block * 100), Fmt((ushort)tag, channels, rate, bits), Chunk("data", (uint)(block * 100)));

        Assert.Null(Problem(file));
    }

    /// <summary>A fmt chunk behind other chunks, and one that lies beyond the head, are found or left to the server.</summary>
    [Fact]
    public void AFmtChunkBehindOtherChunksIsFoundAndOneBeyondTheHeadIsLeftToTheServer()
    {
        byte[] junk = Chunk("JUNK", 28, 28);
        (byte[], long) behindJunk = File(12 + junk.Length + 24 + 8 + 4000, junk, Fmt(1, 2, 44100, 16), Chunk("data", 4000));
        byte[] huge = Chunk("bext", 100_000, 4000);
        (byte[], long) beyond = File(12 + 8 + 100_000 + 24 + 8 + 4000, huge);
        (byte[], long) eightBit = File(12 + junk.Length + 24 + 8 + 4000, junk, Fmt(1, 2, 44100, 8), Chunk("data", 4000));

        Assert.Null(Problem(behindJunk));
        Assert.Null(Problem(beyond));
        Assert.StartsWith("The samples are not in a format", Problem(eightBit), StringComparison.Ordinal);
    }

    /// <summary>What the server refuses for its samples or its numbers is refused here, with the reason.</summary>
    [Theory]
    [InlineData(1, 2, 44100, 8, "The samples are not in a format that is accepted")]
    [InlineData(2, 2, 44100, 4, "The samples are not in a format that is accepted")]
    [InlineData(3, 2, 44100, 64, "The samples are not in a format that is accepted")]
    [InlineData(1, 0, 44100, 16, "The file has 0 channels; 1 to 8 are accepted.")]
    [InlineData(1, 9, 44100, 16, "The file has 9 channels; 1 to 8 are accepted.")]
    [InlineData(1, 2, 7999, 16, "The sample rate is 7999 Hz; 8000 to 384000 Hz are accepted.")]
    [InlineData(1, 2, 384001, 16, "The sample rate is 384001 Hz; 8000 to 384000 Hz are accepted.")]
    public void WhatTheServerRefusesForItsSamplesOrNumbersIsRefusedHere(int tag, int channels, int rate, int bits, string expected)
    {
        (byte[], long) file = File(12 + 24 + 8 + 4000, Fmt((ushort)tag, channels, rate, bits, channels * Math.Max(1, bits / 8)), Chunk("data", 4000));

        Assert.StartsWith(expected, Problem(file), StringComparison.Ordinal);
    }

    /// <summary>A block size that does not agree, a data chunk of no frames, of cut frames, with no size or before the format is refused.</summary>
    [Fact]
    public void ABadBlockSizeOrDataChunkIsRefused()
    {
        long total = 12 + 24 + 8 + 4000;

        Assert.Equal(
            "The block size in the fmt chunk does not match its channels and bits.",
            Problem(File(total, Fmt(1, 2, 44100, 16, 3), Chunk("data", 4000))));
        Assert.Equal("The file has no audio data.", Problem(File(12 + 24 + 8, Fmt(1, 2, 44100, 16), Chunk("data", 0))));
        Assert.Equal("The audio data is not a whole number of frames.", Problem(File(total - 1, Fmt(1, 2, 44100, 16), Chunk("data", 3999))));
        Assert.Equal("The audio data is cut short.", Problem(File(total - 100, Fmt(1, 2, 44100, 16), Chunk("data", 4000))));
        Assert.Equal(
            "The size of the audio data is not set, as in a file that was streamed.",
            Problem(File(total, Fmt(1, 2, 44100, 16), Chunk("data", uint.MaxValue))));
        Assert.Equal("The data chunk comes before the fmt chunk.", Problem(File(total, Chunk("data", 4000), Fmt(1, 2, 44100, 16))));
        Assert.Equal("The file has two fmt chunks.", Problem(File(total, Fmt(1, 2, 44100, 16), Fmt(1, 2, 44100, 16), Chunk("data", 4000))));
    }

    /// <summary>The RIFF header is checked first: empty, short, not WAV, RF64, too large for a WAV file, and a size that is not the file's.</summary>
    [Fact]
    public void TheRiffHeaderIsCheckedFirst()
    {
        (byte[] good, long length) = Pcm16();

        Assert.Equal("The file is empty.", WavPrecheck.Problem([], 0));
        Assert.Equal("The file is too short to be a WAV file.", WavPrecheck.Problem(good, 20));
        Assert.StartsWith("Only WAV files", WavPrecheck.Problem([.. "OggS"u8, .. good[4..]], length), StringComparison.Ordinal);
        Assert.StartsWith("Only WAV files", WavPrecheck.Problem([.. good[..8], .. "AVI "u8, .. good[12..]], length), StringComparison.Ordinal);
        Assert.Equal("RF64 files are not accepted.", WavPrecheck.Problem([.. "RF64"u8, .. good[4..]], length));
        Assert.StartsWith("The file is cut short", WavPrecheck.Problem(good, length + 1), StringComparison.Ordinal);
        Assert.StartsWith("The file is cut short", WavPrecheck.Problem(good, length - 1), StringComparison.Ordinal);
        Assert.Contains("too large", WavPrecheck.Problem(good, 5L * 1024 * 1024 * 1024), StringComparison.Ordinal);
    }
}
