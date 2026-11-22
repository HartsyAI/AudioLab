using System.Buffers.Binary;
using AudioLab.Core.Model;

namespace AudioLab.Server.Tests;

/// <summary>Builds WAV files for tests, whole or in the pieces a test can break.</summary>
public static class TestWav
{
    /// <summary>A whole valid file; <paramref name="signal"/> gives each sample from -1 to 1 by frame and channel.</summary>
    public static byte[] Build(
        SampleEncoding encoding,
        int channels,
        int rate,
        int frames,
        Func<int, int, double>? signal = null,
        bool extensible = false)
    {
        signal ??= (frame, _) => Math.Sin(frame * 0.05) * 0.5;
        return Riff(Format(encoding, channels, rate, extensible), Chunk("data", Samples(encoding, channels, frames, signal)));
    }

    /// <summary>A whole file whose samples are exactly <paramref name="samples"/> (interleaved 16-bit values) in the encoding asked for.</summary>
    public static byte[] FromShorts(SampleEncoding encoding, int channels, int rate, short[] samples)
    {
        int frames = samples.Length / channels;
        byte[] data = Samples(encoding, channels, frames, (frame, channel) => 0);
        int width = data.Length / samples.Length;
        for (int index = 0; index < samples.Length; index++)
        {
            Span<byte> target = data.AsSpan(index * width, width);
            switch (encoding)
            {
                case SampleEncoding.Pcm16:
                    BinaryPrimitives.WriteInt16LittleEndian(target, samples[index]);
                    break;
                case SampleEncoding.Pcm24:
                    target[0] = 0;
                    BinaryPrimitives.WriteInt16LittleEndian(target[1..], samples[index]);
                    break;
                case SampleEncoding.Pcm32:
                    BinaryPrimitives.WriteInt32LittleEndian(target, samples[index] << 16);
                    break;
                default:
                    BinaryPrimitives.WriteSingleLittleEndian(target, samples[index] / 32768f);
                    break;
            }
        }
        return Riff(Format(encoding, channels, rate), Chunk("data", data));
    }

    public static byte[] Format(SampleEncoding encoding, int channels, int rate, bool extensible = false)
    {
        int bits = encoding switch { SampleEncoding.Pcm16 => 16, SampleEncoding.Pcm24 => 24, _ => 32 };
        ushort tag = encoding == SampleEncoding.Float32 ? (ushort)3 : (ushort)1;
        int blockAlign = channels * (bits / 8);
        if (!extensible)
        {
            return Chunk("fmt ", Fmt(tag, channels, rate, rate * blockAlign, blockAlign, bits));
        }
        byte[] body = new byte[40];
        Fmt(0xFFFE, channels, rate, rate * blockAlign, blockAlign, bits).CopyTo(body, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(16), 22);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(18), (ushort)bits);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(20), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(24), tag);
        byte[] tail = [0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];
        tail.CopyTo(body, 26);
        return Chunk("fmt ", body);
    }

    /// <summary>The 16 bytes every fmt chunk starts with, with any value a test wants to get wrong.</summary>
    public static byte[] Fmt(ushort tag, int channels, int rate, int byteRate, int blockAlign, int bits)
    {
        byte[] body = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(0), tag);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(2), (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4), (uint)rate);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(8), (uint)byteRate);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(12), (ushort)blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(14), (ushort)bits);
        return body;
    }

    public static byte[] Chunk(string id, byte[] body)
    {
        byte[] chunk = new byte[8 + body.Length + (body.Length & 1)];
        System.Text.Encoding.ASCII.GetBytes(id).CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), (uint)body.Length);
        body.CopyTo(chunk, 8);
        return chunk;
    }

    /// <summary>Wraps chunks, already padded, in a RIFF header whose size is right.</summary>
    public static byte[] Riff(params byte[][] chunks)
    {
        int size = 4 + chunks.Sum(chunk => chunk.Length);
        byte[] file = new byte[8 + size];
        "RIFF"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), (uint)size);
        "WAVE"u8.CopyTo(file.AsSpan(8));
        int offset = 12;
        foreach (byte[] chunk in chunks)
        {
            chunk.CopyTo(file, offset);
            offset += chunk.Length;
        }
        return file;
    }

    public static byte[] Samples(SampleEncoding encoding, int channels, int frames, Func<int, int, double> signal)
    {
        int width = encoding switch { SampleEncoding.Pcm16 => 2, SampleEncoding.Pcm24 => 3, _ => 4 };
        byte[] data = new byte[frames * channels * width];
        int offset = 0;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                double value = signal(frame, channel);
                Span<byte> target = data.AsSpan(offset, width);
                switch (encoding)
                {
                    case SampleEncoding.Pcm16:
                        BinaryPrimitives.WriteInt16LittleEndian(target, (short)Math.Round(value * 32767));
                        break;
                    case SampleEncoding.Pcm24:
                        int v24 = (int)Math.Round(value * 8388607);
                        target[0] = (byte)v24;
                        target[1] = (byte)(v24 >> 8);
                        target[2] = (byte)(v24 >> 16);
                        break;
                    case SampleEncoding.Pcm32:
                        BinaryPrimitives.WriteInt32LittleEndian(target, (int)Math.Round(value * 2147483647));
                        break;
                    default:
                        BinaryPrimitives.WriteSingleLittleEndian(target, (float)value);
                        break;
                }
                offset += width;
            }
        }
        return data;
    }
}
