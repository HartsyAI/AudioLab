using System.Buffers.Binary;
using System.Text;

namespace AudioLab.Audio.Tests.Support;

/// <summary>A small reader of the WAV files the engine writes, written apart from the writer so that the two check each other.</summary>
public sealed class WavFile
{
    private readonly byte[] _bytes;
    private readonly int _dataStart;

    public WavFile(byte[] bytes)
    {
        _bytes = bytes;
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal((uint)(bytes.Length - 8), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        int at = 12;
        while (at + 8 <= bytes.Length)
        {
            string id = Encoding.ASCII.GetString(bytes, at, 4);
            int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at + 4));
            ReadOnlySpan<byte> body = bytes.AsSpan(at + 8, Math.Min(size, bytes.Length - at - 8));
            switch (id)
            {
                case "fmt ":
                    FormatTag = BinaryPrimitives.ReadUInt16LittleEndian(body);
                    Channels = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                    SampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
                    ByteRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[8..]);
                    BlockAlign = BinaryPrimitives.ReadUInt16LittleEndian(body[12..]);
                    Bits = BinaryPrimitives.ReadUInt16LittleEndian(body[14..]);
                    FormatSize = size;
                    break;
                case "fact":
                    FactFrames = (int)BinaryPrimitives.ReadUInt32LittleEndian(body);
                    break;
                case "data":
                    _dataStart = at + 8;
                    DataSize = size;
                    break;
            }
            at += 8 + size + (size & 1);
        }
        Assert.NotEqual(0, _dataStart);
        Assert.Equal(bytes.Length, _dataStart + DataSize);
    }

    public int FormatTag { get; }

    public int Channels { get; }

    public int SampleRate { get; }

    public int ByteRate { get; }

    public int BlockAlign { get; }

    public int Bits { get; }

    public int FormatSize { get; }

    public int? FactFrames { get; }

    public int DataSize { get; }

    public int Frames => DataSize / BlockAlign;

    /// <summary>The integer value of a sample, or the bits of a float one.</summary>
    public long Raw(int frame, int channel)
    {
        int at = _dataStart + (frame * BlockAlign) + (channel * Bits / 8);
        return Bits switch
        {
            16 => BinaryPrimitives.ReadInt16LittleEndian(_bytes.AsSpan(at)),
            24 => (_bytes[at] | (_bytes[at + 1] << 8) | ((sbyte)_bytes[at + 2] << 16)),
            _ => BinaryPrimitives.ReadInt32LittleEndian(_bytes.AsSpan(at)),
        };
    }

    public float Float(int frame, int channel) => BitConverter.Int32BitsToSingle((int)Raw(frame, channel));
}
