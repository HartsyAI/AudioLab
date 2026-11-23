using System.Buffers.Binary;
using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>Turns the samples of a checked WAV file into floats from -1 up to, but not including, 1; a float file keeps its own values.</summary>
public static class WavSamples
{
    /// <summary>Decodes <paramref name="source"/>, whole samples of <paramref name="encoding"/>, into <paramref name="destination"/>.</summary>
    /// <exception cref="ArgumentException">The source is not whole samples, or the destination is too short.</exception>
    public static void ToFloat(ReadOnlySpan<byte> source, SampleEncoding encoding, Span<float> destination)
    {
        int width = BytesPerSample(encoding);
        if (source.Length % width != 0 || destination.Length < source.Length / width)
        {
            throw new ArgumentException("The source must be whole samples and the destination must hold them all.");
        }
        int count = source.Length / width;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> sample = source.Slice(index * width, width);
            destination[index] = encoding switch
            {
                SampleEncoding.Pcm16 => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768f,
                SampleEncoding.Pcm24 => (((sample[0] | (sample[1] << 8) | (sample[2] << 16)) << 8) >> 8) / 8388608f,
                SampleEncoding.Pcm32 => (float)(BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648.0),
                _ => BinaryPrimitives.ReadSingleLittleEndian(sample),
            };
        }
    }

    /// <summary>The size of one sample, in bytes.</summary>
    public static int BytesPerSample(SampleEncoding encoding)
    {
        return encoding switch
        {
            SampleEncoding.Pcm16 => 2,
            SampleEncoding.Pcm24 => 3,
            _ => 4,
        };
    }
}
