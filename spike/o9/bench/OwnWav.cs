using System.Buffers.Binary;

namespace O9;

/// <summary>The straightforward version: walk the chunks, decode PCM 16, PCM 24 and float 32, write mono 16.</summary>
public static class OwnWav
{
    public static byte[] Build(float[][] channels, int rate, int bits, bool isFloat)
    {
        int ch = channels.Length;
        int frames = channels[0].Length;
        int bytesPer = bits / 8;
        int dataBytes = frames * ch * bytesPer;
        byte[] b = new byte[44 + dataBytes];
        "RIFF"u8.CopyTo(b);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), (uint)(36 + dataBytes));
        "WAVEfmt "u8.CopyTo(b.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(20), (ushort)(isFloat ? 3 : 1));
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(22), (ushort)ch);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(24), (uint)rate);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(28), (uint)(rate * ch * bytesPer));
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(32), (ushort)(ch * bytesPer));
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(34), (ushort)bits);
        "data"u8.CopyTo(b.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(40), (uint)dataBytes);
        int o = 44;
        for (int f = 0; f < frames; f++)
        {
            for (int c = 0; c < ch; c++)
            {
                float v = channels[c][f];
                if (isFloat)
                {
                    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o), v);
                }
                else if (bits == 16)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(o), (short)Math.Clamp(MathF.Round(v * 32767f), -32768f, 32767f));
                }
                else
                {
                    int s = (int)Math.Clamp(MathF.Round(v * 8388607f), -8388608f, 8388607f);
                    b[o] = (byte)s;
                    b[o + 1] = (byte)(s >> 8);
                    b[o + 2] = (byte)(s >> 16);
                }
                o += bytesPer;
            }
        }
        return b;
    }

    public static (float[][] Channels, int Rate) Read(ReadOnlySpan<byte> b)
    {
        if (b.Length < 12 || !b[..4].SequenceEqual("RIFF"u8) || !b[8..12].SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("not a RIFF/WAVE file");
        }
        int pos = 12, tag = 0, ch = 0, rate = 0, bits = 0;
        while (pos + 8 <= b.Length)
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(b[(pos + 4)..]);
            int body = pos + 8;
            if (b.Slice(pos, 4).SequenceEqual("fmt "u8))
            {
                tag = BinaryPrimitives.ReadUInt16LittleEndian(b[body..]);
                ch = BinaryPrimitives.ReadUInt16LittleEndian(b[(body + 2)..]);
                rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(b[(body + 4)..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(b[(body + 14)..]);
            }
            else if (b.Slice(pos, 4).SequenceEqual("data"u8))
            {
                int bytesPer = bits / 8;
                int frames = (int)size / (ch * bytesPer);
                float[][] x = new float[ch][];
                for (int c = 0; c < ch; c++)
                {
                    x[c] = new float[frames];
                }
                ReadOnlySpan<byte> d = b.Slice(body, frames * ch * bytesPer);
                for (int f = 0; f < frames; f++)
                {
                    for (int c = 0; c < ch; c++)
                    {
                        ReadOnlySpan<byte> s = d[((f * ch + c) * bytesPer)..];
                        x[c][f] = tag == 3 ? BinaryPrimitives.ReadSingleLittleEndian(s)
                            : bits == 16 ? BinaryPrimitives.ReadInt16LittleEndian(s) / 32768f
                            : (((s[0] | (s[1] << 8) | (s[2] << 16)) << 8) >> 8) / 8388608f;
                    }
                }
                return (x, rate);
            }
            pos = body + (int)size + (int)(size & 1);
        }
        throw new InvalidDataException("no data chunk");
    }

    public static bool TryDuration(ReadOnlySpan<byte> b, out double seconds)
    {
        seconds = 0;
        if (b.Length < 44)
        {
            return false;
        }
        int ch = BinaryPrimitives.ReadUInt16LittleEndian(b[22..]);
        int rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(b[24..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(b[34..]);
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(b[40..]);
        seconds = size / (double)(ch * (bits / 8)) / rate;
        return true;
    }

    public static byte[] WriteMono16(ReadOnlySpan<float> samples, int rate) => Build([samples.ToArray()], rate, 16, false);
}
