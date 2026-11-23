using System.Buffers.Binary;

namespace AudioLab.Core.Model;

/// <summary>The peak file: what the server stores beside each media file and serves for waveforms, and how to read and write it.</summary>
/// <remarks>
/// Little endian. A header of 24 bytes: <c>ALPK</c>, the version (u16), channels (u16), sample rate (u32), frames (u64), the
/// level count (u16) and a reserved u16 that is 0. Then one entry for each level, finest first: frames per peak (u32) and peak
/// count (u32). Then the values of each level in the same order: for each peak, for each channel, the minimum then the maximum
/// as signed 16-bit integers, where a sample is its value times 32768 rounded to the nearest even and kept from -32768 to 32767.
/// A 16-bit file's peaks are therefore its exact samples. The reader refuses any file that is not exactly what its header says.
/// </remarks>
public static class PeakFile
{
    /// <summary>The version this code reads and writes.</summary>
    public const int Version = 1;

    /// <summary>The size of the fixed header, in bytes.</summary>
    public const int HeaderBytes = 24;

    /// <summary>The size of one level's entry in the table that follows the header, in bytes.</summary>
    public const int LevelEntryBytes = 8;

    /// <summary>The most levels a file may have.</summary>
    public const int MaxLevels = 16;

    /// <summary>The media type the server answers a peak file with.</summary>
    public const string MediaType = "application/vnd.audiolab.peaks";

    private static ReadOnlySpan<byte> Magic => "ALPK"u8;

    /// <summary>The zoom levels every file is written with, as frames a peak: the finest first, each four times the one before.</summary>
    public static ReadOnlySpan<int> FramesPerPeak => [64, 256, 1024, 4096, 16384];

    /// <summary>How many peaks a level of <paramref name="framesPerPeak"/> has for a file of <paramref name="frames"/> frames.</summary>
    public static long PeakCount(long frames, int framesPerPeak) => (frames + framesPerPeak - 1) / framesPerPeak;

    /// <summary>The whole file as bytes.</summary>
    public static byte[] Write(PeakData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int tableEnd = HeaderBytes + (LevelEntryBytes * data.Levels.Count);
        long total = tableEnd + data.Levels.Sum(level => (long)level.Values.Length * sizeof(short));
        byte[] bytes = new byte[total];
        Magic.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), Version);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), (ushort)data.Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)data.SampleRate);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(12), (ulong)data.Frames);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), (ushort)data.Levels.Count);
        int entry = HeaderBytes;
        int offset = tableEnd;
        foreach (PeakLevel level in data.Levels)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entry), (uint)level.FramesPerPeak);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entry + 4), (uint)level.PeakCount(data.Channels));
            entry += LevelEntryBytes;
            foreach (short value in level.Values)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
                offset += sizeof(short);
            }
        }
        return bytes;
    }

    /// <summary>Reads a whole file and checks it against its own header.</summary>
    /// <exception cref="InvalidDataException">The bytes are not a peak file of this version, or are cut, padded or inconsistent.</exception>
    public static PeakData Read(ReadOnlySpan<byte> bytes)
    {
        PeakHeader header = ReadHeader(bytes);
        if (bytes.Length != header.TotalBytes)
        {
            throw new InvalidDataException($"The peak file is {bytes.Length} bytes but its header says {header.TotalBytes}.");
        }
        List<PeakLevel> levels = [];
        int offset = header.TableEnd;
        foreach ((int framesPerPeak, long peaks) in header.Levels)
        {
            short[] values = new short[peaks * header.Channels * 2];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
                offset += sizeof(short);
            }
            levels.Add(new PeakLevel(framesPerPeak, values));
        }
        return new PeakData(header.Channels, header.SampleRate, header.Frames, levels);
    }

    /// <summary>Reads and checks the header and level table, which is enough to know how long the whole file must be.</summary>
    /// <param name="bytes">The start of the file, at least as far as the level table.</param>
    /// <exception cref="InvalidDataException">The header is not that of a peak file of this version.</exception>
    public static PeakHeader ReadHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderBytes || !bytes[..4].SequenceEqual(Magic))
        {
            throw new InvalidDataException("The bytes are not a peak file.");
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..6]) != Version)
        {
            throw new InvalidDataException($"The peak file is not version {Version}.");
        }
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..8]);
        uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..12]);
        ulong frames = BinaryPrimitives.ReadUInt64LittleEndian(bytes[12..20]);
        int levelCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[20..22]);
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[22..24]) != 0)
        {
            throw new InvalidDataException("The reserved field of the peak file is not 0.");
        }
        if (channels is < 1 or > MediaValidator.MaxChannels
            || sampleRate is < MediaValidator.MinSampleRate or > MediaValidator.MaxSampleRate
            || frames is 0 or > long.MaxValue)
        {
            throw new InvalidDataException("The peak file names a channel count, sample rate or length that no media file has.");
        }
        if (levelCount is < 1 or > MaxLevels || bytes.Length < HeaderBytes + (LevelEntryBytes * levelCount))
        {
            throw new InvalidDataException("The peak file's level table is missing or has an impossible length.");
        }
        List<(int, long)> levels = [];
        long total = HeaderBytes + (LevelEntryBytes * levelCount);
        int previous = 0;
        for (int index = 0; index < levelCount; index++)
        {
            ReadOnlySpan<byte> entry = bytes.Slice(HeaderBytes + (LevelEntryBytes * index), LevelEntryBytes);
            uint framesPerPeak = BinaryPrimitives.ReadUInt32LittleEndian(entry[..4]);
            uint peaks = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..8]);
            if (framesPerPeak == 0 || framesPerPeak > int.MaxValue || framesPerPeak <= previous)
            {
                throw new InvalidDataException("The peak levels are not in order from the finest to the coarsest.");
            }
            if (peaks != PeakCount((long)frames, (int)framesPerPeak))
            {
                throw new InvalidDataException("A peak level does not have as many peaks as its frames need.");
            }
            previous = (int)framesPerPeak;
            levels.Add(((int)framesPerPeak, peaks));
            total += (long)peaks * channels * 2 * sizeof(short);
        }
        return new PeakHeader(channels, (int)sampleRate, (long)frames, levels, HeaderBytes + (LevelEntryBytes * levelCount), total);
    }
}
