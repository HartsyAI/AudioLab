using System.Buffers.Binary;
using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The part of the server's WAV check (D37) that the start of a file can answer, made before a large file is sent.</summary>
/// <remarks>
/// It reads the RIFF header, then the chunks that lie inside the first <see cref="HeadBytes"/>: the fmt chunk's format, bits, channels, rate
/// and block size, and the data chunk's size when its header is there too. A file whose fmt chunk lies further in, behind a large chunk, passes
/// this check and is judged by the server, which checks the whole file. What this refuses is a subset of what the server refuses.
/// </remarks>
public static class WavPrecheck
{
    /// <summary>The bytes the check needs from the start of the file, or all of it when it is shorter.</summary>
    public const int HeadBytes = 4096;

    private const long SmallestFile = 44;

    private const ushort TagPcm = 1;

    private const ushort TagFloat = 3;

    private const ushort TagExtensible = 0xFFFE;

    private const string Accepted = "only WAV with 16, 24 or 32-bit PCM or 32-bit float samples is accepted";

    /// <summary>Why the file cannot be a WAV file the server accepts, or null when what this can see passes; the server checks the rest.</summary>
    /// <param name="head">The first <see cref="HeadBytes"/> bytes of the file, or all of it when it is shorter.</param>
    /// <param name="length">The size of the whole file.</param>
    public static string? Problem(ReadOnlySpan<byte> head, long length)
    {
        if (length < SmallestFile)
        {
            return length == 0 ? "The file is empty." : "The file is too short to be a WAV file.";
        }
        if (head.Length >= 4 && (head[..4].SequenceEqual("RF64"u8) || head[..4].SequenceEqual("BW64"u8)))
        {
            return "RF64 files are not accepted.";
        }
        if (head.Length < 12 || !head[..4].SequenceEqual("RIFF"u8) || !head[8..12].SequenceEqual("WAVE"u8))
        {
            return "Only WAV files can be added, and this is not one.";
        }
        if (length - 8 > uint.MaxValue)
        {
            return "The file is too large to be a WAV file: a WAV file cannot be more than 4 GiB.";
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(head[4..8]) != length - 8)
        {
            return "The file is cut short or has extra bytes after its end: its header does not match its size.";
        }
        return Chunks(head, length);
    }

    private static string? Chunks(ReadOnlySpan<byte> head, long length)
    {
        int block = 0;
        bool format = false;
        int position = 12;
        while (position + 8 <= head.Length)
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(head[(position + 4)..]);
            ReadOnlySpan<byte> id = head.Slice(position, 4);
            if (id.SequenceEqual("fmt "u8))
            {
                if (format)
                {
                    return "The file has two fmt chunks.";
                }
                if (position + 8 + 16 > head.Length)
                {
                    return null;
                }
                string? bad = Format(head[(position + 8)..], size, out block);
                if (bad is not null)
                {
                    return bad;
                }
                format = true;
            }
            else if (id.SequenceEqual("data"u8))
            {
                return format ? Data(size, position + 8L, length, block) : "The data chunk comes before the fmt chunk.";
            }
            long next = position + 8L + size + (size & 1);
            if (next > head.Length)
            {
                return null;
            }
            position = (int)next;
        }
        return null;
    }

    private static string? Format(ReadOnlySpan<byte> fmt, uint size, out int block)
    {
        block = 0;
        ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
        int rate = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..]), int.MaxValue);
        int align = BinaryPrimitives.ReadUInt16LittleEndian(fmt[12..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);
        if (tag == TagExtensible)
        {
            if (size < 40 || fmt.Length < 26)
            {
                return "The fmt chunk of an extensible file is too short.";
            }
            tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt[24..]);
        }
        bool accepted = (tag == TagPcm && bits is 16 or 24 or 32) || (tag == TagFloat && bits == 32);
        if (!accepted)
        {
            return $"The samples are not in a format that is accepted: {Accepted}.";
        }
        if (channels is < 1 or > MediaValidator.MaxChannels)
        {
            return string.Create(CultureInfo.InvariantCulture, $"The file has {channels} channels; 1 to {MediaValidator.MaxChannels} are accepted.");
        }
        if (rate is < MediaValidator.MinSampleRate or > MediaValidator.MaxSampleRate)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The sample rate is {rate} Hz; {MediaValidator.MinSampleRate} to {MediaValidator.MaxSampleRate} Hz are accepted.");
        }
        if (align != channels * (bits / 8))
        {
            return "The block size in the fmt chunk does not match its channels and bits.";
        }
        block = align;
        return null;
    }

    // A WAV file cannot hold more than a project may, whatever its rate, so its length in seconds needs no check here.
    private static string? Data(uint size, long start, long length, int block)
    {
        if (size == uint.MaxValue)
        {
            return "The size of the audio data is not set, as in a file that was streamed.";
        }
        if (size == 0)
        {
            return "The file has no audio data.";
        }
        if (start + size > length)
        {
            return "The audio data is cut short.";
        }
        return size % block != 0 ? "The audio data is not a whole number of frames." : null;
    }
}
