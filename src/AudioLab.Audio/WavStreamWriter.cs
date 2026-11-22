using System.Buffers.Binary;
using System.Text;

namespace AudioLab.Audio;

/// <summary>Writes stereo float audio to a stream as a WAV file, block by block, with the header sized from the frame count given up front.</summary>
/// <remarks>
/// Integer samples are scaled by the power of two that fills the range, rounded (after dither, when asked) and clamped; a clamped sample
/// is counted in <see cref="ClippedSamples"/>. The file is plain RIFF, so it holds at most 4 GiB.
/// </remarks>
public sealed class WavStreamWriter
{
    private const int Channels = 2;
    private const int ChunkFrames = 4096;

    private readonly Stream _stream;
    private readonly WavFormat _format;
    private readonly DitherKind _dither;
    private readonly long _totalFrames;
    private readonly int _bytesPerSample;
    private readonly byte[] _buffer;
    private uint _stateLeft;
    private uint _stateRight;
    private long _written;

    /// <summary>Writes the header of a file of <paramref name="frames"/> stereo frames at <paramref name="sampleRate"/>.</summary>
    public WavStreamWriter(Stream stream, int sampleRate, long frames, WavOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, AudioFormat.MinSampleRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, AudioFormat.MaxSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        _stream = stream;
        _format = options.Format;
        _dither = options.Dither;
        _totalFrames = frames;
        _bytesPerSample = options.Format switch
        {
            WavFormat.Pcm16 => 2,
            WavFormat.Pcm24 => 3,
            _ => 4,
        };
        if (RiffSize(frames * Channels * _bytesPerSample, options.Format == WavFormat.Float32) > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(frames), "A WAV file cannot hold more than 4 GiB.");
        }
        _buffer = new byte[ChunkFrames * Channels * _bytesPerSample];
        // Two generators that never share a state, however the seed is chosen.
        _stateLeft = (options.Seed * 2) + 0x9E3779B9;
        _stateRight = (options.Seed * 2) + 0x7F4A7C15;
        WriteHeader(sampleRate);
    }

    /// <summary>How many samples were pulled back to the edge of the integer range because they went past it.</summary>
    public long ClippedSamples { get; private set; }

    /// <summary>How many samples were NaN or infinite.</summary>
    public long NonFiniteSamples { get; private set; }

    /// <summary>What was found in the samples written so far.</summary>
    public WavResult Result => new(ClippedSamples, NonFiniteSamples);

    /// <summary>Writes the next frames, which must not run past the count given to the constructor.</summary>
    public void Write(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("The left and right buffers must have the same length.", nameof(right));
        }
        if (_written + left.Length > _totalFrames)
        {
            throw new InvalidOperationException("More frames were written than the header announced.");
        }
        for (int from = 0; from < left.Length; from += ChunkFrames)
        {
            int count = Math.Min(ChunkFrames, left.Length - from);
            Encode(left.Slice(from, count), right.Slice(from, count));
            _stream.Write(_buffer, 0, count * Channels * _bytesPerSample);
        }
        _written += left.Length;
    }

    /// <summary>Checks that every announced frame was written, and flushes the stream.</summary>
    public void Complete()
    {
        if (_written != _totalFrames)
        {
            throw new InvalidOperationException($"The header announced {_totalFrames} frames and {_written} were written.");
        }
        _stream.Flush();
    }

    // A float file has an 18-byte format chunk and a fact chunk, as the format asks of anything that is not PCM.
    private static long RiffSize(long dataSize, bool isFloat) => 4 + (8 + (isFloat ? 18 : 16)) + (isFloat ? 8 + 4 : 0) + 8 + dataSize;

    private void WriteHeader(int sampleRate)
    {
        long dataSize = _totalFrames * Channels * _bytesPerSample;
        bool isFloat = _format == WavFormat.Float32;
        long riffSize = RiffSize(dataSize, isFloat);
        byte[] header = new byte[isFloat ? 58 : 44];
        Span<byte> bytes = header;
        int at = 0;
        WriteText(bytes, ref at, "RIFF");
        WriteUInt32(bytes, ref at, (uint)riffSize);
        WriteText(bytes, ref at, "WAVE");
        WriteText(bytes, ref at, "fmt ");
        WriteUInt32(bytes, ref at, isFloat ? 18u : 16u);
        WriteUInt16(bytes, ref at, isFloat ? (ushort)3 : (ushort)1);
        WriteUInt16(bytes, ref at, Channels);
        WriteUInt32(bytes, ref at, (uint)sampleRate);
        WriteUInt32(bytes, ref at, (uint)(sampleRate * Channels * _bytesPerSample));
        WriteUInt16(bytes, ref at, (ushort)(Channels * _bytesPerSample));
        WriteUInt16(bytes, ref at, (ushort)(_bytesPerSample * 8));
        if (isFloat)
        {
            WriteUInt16(bytes, ref at, 0);
            WriteText(bytes, ref at, "fact");
            WriteUInt32(bytes, ref at, 4);
            WriteUInt32(bytes, ref at, (uint)_totalFrames);
        }
        WriteText(bytes, ref at, "data");
        WriteUInt32(bytes, ref at, (uint)dataSize);
        _stream.Write(header, 0, header.Length);
    }

    private void Encode(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        Span<byte> bytes = _buffer;
        int at = 0;
        for (int i = 0; i < left.Length; i++)
        {
            at = Put(bytes, at, left[i], ref _stateLeft);
            at = Put(bytes, at, right[i], ref _stateRight);
        }
    }

    private int Put(Span<byte> bytes, int at, float sample, ref uint state)
    {
        if (!float.IsFinite(sample))
        {
            NonFiniteSamples++;
        }
        if (_format == WavFormat.Float32)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes[at..], sample);
            return at + 4;
        }
        int bits = _format == WavFormat.Pcm16 ? 16 : 24;
        long maximum = (1L << (bits - 1)) - 1;
        double value = float.IsFinite(sample) ? sample * (1L << (bits - 1)) : 0;
        value += _dither switch
        {
            DitherKind.Rectangular => Next(ref state) - 0.5,
            DitherKind.Triangular => Next(ref state) - Next(ref state),
            _ => 0,
        };
        long integer = (long)Math.Floor(value + 0.5);
        if (integer > maximum || integer < -maximum - 1)
        {
            integer = Math.Clamp(integer, -maximum - 1, maximum);
            ClippedSamples++;
        }
        bytes[at] = (byte)integer;
        bytes[at + 1] = (byte)(integer >> 8);
        if (bits == 24)
        {
            bytes[at + 2] = (byte)(integer >> 16);
        }
        return at + (bits / 8);
    }

    // A uniform number from 0 up to but not including 1, from a xorshift generator that gives the same numbers on every platform.
    private static double Next(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state >> 8) / 16777216.0;
    }

    private static void WriteText(Span<byte> bytes, ref int at, string text)
    {
        at += Encoding.ASCII.GetBytes(text, bytes[at..]);
    }

    private static void WriteUInt32(Span<byte> bytes, ref int at, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);
        at += 4;
    }

    private static void WriteUInt16(Span<byte> bytes, ref int at, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[at..], value);
        at += 2;
    }
}
