using System.Buffers.Binary;

namespace AudioLab.Web.Services;

/// <summary>The cheap part of the server's WAV check (D37), made before a large file is sent: the RIFF header and its size.</summary>
public static class WavPrecheck
{
    /// <summary>The bytes the check needs from the start of the file.</summary>
    public const int HeadBytes = 12;

    private const long SmallestFile = 44;

    /// <summary>Why the file cannot be a WAV file the server accepts, or null when its header passes; the server still checks the rest.</summary>
    /// <param name="head">The first <see cref="HeadBytes"/> bytes of the file, or all of it when it is shorter.</param>
    /// <param name="length">The size of the whole file.</param>
    public static string? Problem(ReadOnlySpan<byte> head, long length)
    {
        if (length < SmallestFile)
        {
            return length == 0 ? "The file is empty." : "The file is too short to be a WAV file.";
        }
        if (head.Length < HeadBytes || !head[..4].SequenceEqual("RIFF"u8) || !head[8..12].SequenceEqual("WAVE"u8))
        {
            return "Only WAV files can be added, and this is not one.";
        }
        long riffSize = BinaryPrimitives.ReadUInt32LittleEndian(head[4..8]);
        return riffSize == length - 8
            ? null
            : "The file is cut short or has extra bytes after its end: its header does not match its size.";
    }
}
