using System.Buffers.Binary;

namespace AudioLab.Audio.Live;

/// <summary>Decodes a WAV file the media store served into float samples, a slice at a time.</summary>
/// <remarks>
/// The server has already checked every file it serves (D37), so this finds the format and the samples and refuses what it cannot
/// decode (integer PCM of 16, 24 or 32 bits and 32-bit float, plain or extensible); it does not judge a file as the store does.
/// </remarks>
public static class WavDecoder
{
    private const int SliceFrames = 16384;
    private const ushort TagPcm = 1;
    private const ushort TagFloat = 3;
    private const ushort TagExtensible = 0xFFFE;

    /// <summary>Decodes <paramref name="file"/> to one array of samples a channel, calling <paramref name="pause"/> between slices.</summary>
    /// <exception cref="WavFormatException">The file is not a WAV file of a format this decodes.</exception>
    public static async Task<PcmAudio> DecodeAsync(byte[] file, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(pause);
        (int channels, int rate, int bits, bool isFloat, int dataOffset, int dataBytes) = ReadHeader(file);
        int width = bits / 8;
        int frames = dataBytes / (width * channels);
        float[][] samples = new float[channels][];
        for (int channel = 0; channel < channels; channel++)
        {
            samples[channel] = new float[frames];
        }
        for (int at = 0; at < frames; at += SliceFrames)
        {
            int count = Math.Min(SliceFrames, frames - at);
            Fill(file.AsSpan(dataOffset + (at * width * channels)), samples, at, count, width, isFloat);
            await pause(cancellationToken);
        }
        return new PcmAudio(rate, samples);
    }

    private static (int Channels, int Rate, int Bits, bool IsFloat, int DataOffset, int DataBytes) ReadHeader(byte[] file)
    {
        ReadOnlySpan<byte> bytes = file;
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes[8..12].SequenceEqual("WAVE"u8))
        {
            throw new WavFormatException("The file is not a RIFF WAVE file.");
        }
        (int channels, int rate, int bits, bool isFloat)? format = null;
        int position = 12;
        while (position + 8 <= bytes.Length)
        {
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(position + 4, 4)));
            int start = position + 8;
            if (bytes.Slice(position, 4).SequenceEqual("fmt "u8))
            {
                format = ReadFormat(bytes.Slice(start, Math.Min(size, bytes.Length - start)));
            }
            else if (bytes.Slice(position, 4).SequenceEqual("data"u8))
            {
                if (format is not { } known)
                {
                    throw new WavFormatException("The data chunk comes before the fmt chunk.");
                }
                int available = Math.Min(size, bytes.Length - start);
                int frameBytes = known.bits / 8 * known.channels;
                return (known.channels, known.rate, known.bits, known.isFloat, start, available / frameBytes * frameBytes);
            }
            position = start + size + (size & 1);
        }
        throw new WavFormatException("The file has no audio data.");
    }

    private static (int Channels, int Rate, int Bits, bool IsFloat) ReadFormat(ReadOnlySpan<byte> fmt)
    {
        if (fmt.Length < 16)
        {
            throw new WavFormatException("The fmt chunk is shorter than 16 bytes.");
        }
        ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
        int rate = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..]));
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);
        if (tag == TagExtensible && fmt.Length >= 26)
        {
            tag = BinaryPrimitives.ReadUInt16LittleEndian(fmt[24..]);
        }
        bool isFloat = tag == TagFloat && bits == 32;
        bool isPcm = tag == TagPcm && bits is 16 or 24 or 32;
        if (!isFloat && !isPcm)
        {
            throw new WavFormatException($"Samples of tag {tag} and {bits} bits are not decoded: only 16, 24 and 32-bit PCM and 32-bit float.");
        }
        if (channels is < 1 or > AudioFormat.MaxChannels || rate is < AudioFormat.MinSampleRate or > AudioFormat.MaxSampleRate)
        {
            throw new WavFormatException($"{channels} channels at {rate} hertz is outside what the engine plays.");
        }
        return (channels, rate, bits, isFloat);
    }

    private static void Fill(ReadOnlySpan<byte> source, float[][] destination, int at, int count, int width, bool isFloat)
    {
        int channels = destination.Length;
        for (int frame = 0; frame < count; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                ReadOnlySpan<byte> sample = source.Slice(((frame * channels) + channel) * width, width);
                destination[channel][at + frame] = width switch
                {
                    2 => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768f,
                    3 => (((sample[0] | (sample[1] << 8) | (sample[2] << 16)) << 8) >> 8) / 8388608f,
                    _ when isFloat => BinaryPrimitives.ReadSingleLittleEndian(sample),
                    _ => (float)(BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648.0),
                };
            }
        }
    }
}
