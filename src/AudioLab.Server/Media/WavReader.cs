using System.Buffers;
using System.Buffers.Binary;
using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>Reads and checks a RIFF WAVE file: integer PCM of 16, 24 or 32 bits and 32-bit float, nothing else.</summary>
/// <remarks>
/// The file must be exactly what its header says: the RIFF size matches the file, every chunk fits, the fmt chunk comes
/// before the data and its sizes agree, and the data is a whole number of frames. Compressed formats come with the audio
/// engine in phase 6 and are refused here.
/// </remarks>
public static class WavReader
{
    private const int MaxChunks = 256;
    private const int ScanBufferBytes = 64 * 1024;
    private const ushort TagPcm = 1;
    private const ushort TagFloat = 3;
    private const ushort TagExtensible = 0xFFFE;
    private const string Accepted = "only WAV with 16, 24 or 32-bit PCM or 32-bit float samples is accepted";

    // The 14 bytes after the two-byte format tag in the sub-format GUID of every standard WAVE_FORMAT_EXTENSIBLE file.
    private static ReadOnlySpan<byte> SubFormatTail => [0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];

    /// <summary>Checks the file's structure and header and says where its samples are.</summary>
    /// <param name="stream">A seekable stream of the whole file.</param>
    /// <exception cref="MediaRejectedException">The file is not a WAV file this server accepts; the message says why.</exception>
    public static WavFormat Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        long length = stream.Length;
        Span<byte> riff = stackalloc byte[12];
        ReadAt(stream, 0, riff, "the file is shorter than a WAVE header");
        if (riff[..4].SequenceEqual("RF64"u8) || riff[..4].SequenceEqual("BW64"u8))
        {
            throw Refuse("RF64 files are not accepted");
        }
        if (!riff[..4].SequenceEqual("RIFF"u8) || !riff[8..12].SequenceEqual("WAVE"u8))
        {
            throw Refuse("the file is not a RIFF WAVE file");
        }
        long declared = BinaryPrimitives.ReadUInt32LittleEndian(riff[4..8]) + 8L;
        if (declared != length)
        {
            throw Refuse(declared > length ? "the file is truncated: its header says it is longer" : "the file has bytes after the end of its RIFF data");
        }

        FormatChunk? format = null;
        long dataOffset = -1;
        long dataBytes = 0;
        long position = 12;
        int chunks = 0;
        Span<byte> header = stackalloc byte[8];
        while (position < length)
        {
            if (++chunks > MaxChunks)
            {
                throw Refuse($"the file has more than {MaxChunks} chunks");
            }
            ReadAt(stream, position, header, "a chunk header is cut short");
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
            long start = position + 8;
            long end = start + size;
            bool isData = header[..4].SequenceEqual("data"u8);
            if (isData && size == uint.MaxValue)
            {
                throw Refuse("the size of the audio data is not set, as in a file that was streamed");
            }
            if (end > length)
            {
                throw Refuse(isData ? "the audio data is truncated" : "a chunk runs past the end of the file");
            }
            if (header[..4].SequenceEqual("fmt "u8))
            {
                format = format is null ? ReadFormat(stream, start, size) : throw Refuse("the file has two fmt chunks");
            }
            else if (isData)
            {
                if (format is null)
                {
                    throw Refuse("the data chunk comes before the fmt chunk");
                }
                if (dataOffset >= 0)
                {
                    throw Refuse("the file has two data chunks");
                }
                dataOffset = start;
                dataBytes = size;
            }
            // A chunk of odd size is followed by a pad byte; the last chunk may leave it out.
            position = Math.Min(end + (size & 1), length);
        }

        if (format is not { } fmt)
        {
            throw Refuse("the file has no fmt chunk");
        }
        if (dataOffset < 0 || dataBytes == 0)
        {
            throw Refuse("the file has no audio data");
        }
        if (dataBytes % fmt.BlockAlign != 0)
        {
            throw Refuse("the audio data is not a whole number of frames");
        }
        WavFormat result = new(fmt.Encoding, fmt.Channels, fmt.SampleRate, fmt.BlockAlign, dataOffset, dataBytes);
        if (result.Duration > ProjectLimits.MaxSeconds)
        {
            throw Refuse($"the audio is longer than the {ProjectLimits.MaxSeconds} seconds a project may hold");
        }
        return result;
    }

    /// <summary>Refuses a float file whose samples hold a NaN or an infinity, which would poison every mix that plays it.</summary>
    /// <exception cref="MediaRejectedException">A sample is not a finite number.</exception>
    public static async Task RequireFiniteAsync(Stream stream, WavFormat format, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (format.Encoding != SampleEncoding.Float32)
        {
            return;
        }
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ScanBufferBytes);
        try
        {
            stream.Position = format.DataOffset;
            long remaining = format.DataBytes;
            while (remaining > 0)
            {
                int wanted = (int)Math.Min(ScanBufferBytes, remaining);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, wanted), cancellationToken);
                for (int offset = 0; offset < wanted; offset += sizeof(float))
                {
                    if ((BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, sizeof(float))) & 0x7F800000u) == 0x7F800000u)
                    {
                        throw Refuse("a sample is not a finite number");
                    }
                }
                remaining -= wanted;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static FormatChunk ReadFormat(Stream stream, long start, uint size)
    {
        if (size < 16)
        {
            throw Refuse("the fmt chunk is shorter than 16 bytes");
        }
        Span<byte> bytes = stackalloc byte[40];
        bytes.Clear();
        ReadAt(stream, start, bytes[..(int)Math.Min(size, 40)], "the fmt chunk is cut short");
        ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0..2]);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..4]);
        uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..8]);
        uint byteRate = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..12]);
        int blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..14]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes[14..16]);
        if (tag == TagExtensible)
        {
            tag = ReadExtension(bytes, size, bits);
        }
        SampleEncoding encoding = (tag, bits) switch
        {
            (TagPcm, 16) => SampleEncoding.Pcm16,
            (TagPcm, 24) => SampleEncoding.Pcm24,
            (TagPcm, 32) => SampleEncoding.Pcm32,
            (TagFloat, 32) => SampleEncoding.Float32,
            _ => throw Refuse($"the samples are not an accepted format (tag {tag}, {bits} bits); {Accepted}"),
        };
        if (channels is < 1 or > MediaValidator.MaxChannels)
        {
            throw Refuse($"{channels} channels is outside 1 to {MediaValidator.MaxChannels}");
        }
        if (sampleRate is < MediaValidator.MinSampleRate or > MediaValidator.MaxSampleRate)
        {
            throw Refuse($"a sample rate of {sampleRate} is outside {MediaValidator.MinSampleRate} to {MediaValidator.MaxSampleRate}");
        }
        if (blockAlign != channels * (bits / 8))
        {
            throw Refuse("the block size does not match the channels and bits");
        }
        if (byteRate != sampleRate * (uint)blockAlign)
        {
            throw Refuse("the byte rate does not match the sample rate and block size");
        }
        return new FormatChunk(encoding, channels, (int)sampleRate, blockAlign);
    }

    // WAVE_FORMAT_EXTENSIBLE: the real format tag is the start of the sub-format GUID, and the valid bits must fill the container.
    private static ushort ReadExtension(ReadOnlySpan<byte> bytes, uint size, int bits)
    {
        if (size < 40 || BinaryPrimitives.ReadUInt16LittleEndian(bytes[16..18]) != 22)
        {
            throw Refuse("the extensible fmt chunk is not 40 bytes with a 22-byte extension");
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[18..20]) != bits)
        {
            throw Refuse("the valid bits per sample differ from the container size");
        }
        if (!bytes[26..40].SequenceEqual(SubFormatTail))
        {
            throw Refuse("the sub-format is not a standard audio format");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes[24..26]);
    }

    private static void ReadAt(Stream stream, long offset, Span<byte> buffer, string whenShort)
    {
        stream.Position = offset;
        try
        {
            stream.ReadExactly(buffer);
        }
        catch (EndOfStreamException)
        {
            throw Refuse(whenShort);
        }
    }

    private static MediaRejectedException Refuse(string reason) => new($"Not an accepted WAV file: {reason}.");

    private readonly record struct FormatChunk(SampleEncoding Encoding, int Channels, int SampleRate, int BlockAlign);
}
