using System.Buffers.Binary;

namespace AudioLab.Audio;

/// <summary>Reads 16-bit PCM WAV files, mono or stereo, into a stereo clip.</summary>
public static class WavReader
{
    /// <summary>Parses a WAV file; throws <see cref="FormatException" /> for anything that is not 16-bit PCM.</summary>
    public static SampleClip Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < 12 || !file[..4].SequenceEqual("RIFF"u8) || !file.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new FormatException("Not a RIFF/WAVE file.");
        }
        int channels = 0;
        int sampleRate = 0;
        int position = 12;
        while (position + 8 <= file.Length)
        {
            ReadOnlySpan<byte> id = file.Slice(position, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(position + 4, 4));
            int body = position + 8;
            if (size < 0 || body + size > file.Length)
            {
                throw new FormatException("A chunk runs past the end of the file.");
            }
            if (id.SequenceEqual("fmt "u8))
            {
                (channels, sampleRate) = ReadFormat(file.Slice(body, size));
            }
            else if (id.SequenceEqual("data"u8))
            {
                return ReadData(file.Slice(body, size), channels, sampleRate);
            }
            position = body + size + (size & 1);
        }
        throw new FormatException("The file has no data chunk.");
    }

    private static (int Channels, int SampleRate) ReadFormat(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length < 16)
        {
            throw new FormatException("The fmt chunk is too short.");
        }
        int format = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
        int sampleRate = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);
        if (format != 1 || bits != 16 || channels is < 1 or > 2 || sampleRate <= 0)
        {
            throw new FormatException("Only 16-bit PCM, mono or stereo, is supported.");
        }
        return (channels, sampleRate);
    }

    private static SampleClip ReadData(ReadOnlySpan<byte> data, int channels, int sampleRate)
    {
        if (channels == 0)
        {
            throw new FormatException("The data chunk comes before the fmt chunk.");
        }
        int frames = data.Length / (2 * channels);
        short[] samples = new short[frames * 2];
        for (int frame = 0; frame < frames; frame++)
        {
            int offset = frame * channels * 2;
            short left = BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);
            short right = channels == 2 ? BinaryPrimitives.ReadInt16LittleEndian(data[(offset + 2)..]) : left;
            samples[frame * 2] = left;
            samples[(frame * 2) + 1] = right;
        }
        return new SampleClip(samples, sampleRate);
    }
}
