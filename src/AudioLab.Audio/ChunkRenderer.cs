using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AudioLab.Audio;

/// <summary>Renders the engine in fixed chunks into one reused byte buffer: left channel, then right, 32-bit floats.</summary>
public sealed class ChunkRenderer
{
    private readonly AudioEngine _engine;

    private readonly byte[] _bytes;

    private readonly int _chunkFrames;

    private int _stallMilliseconds;

    /// <summary>Creates a renderer producing <paramref name="chunkFrames" /> frames a chunk.</summary>
    public ChunkRenderer(AudioEngine engine, int chunkFrames)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkFrames);
        _engine = engine;
        _chunkFrames = chunkFrames;
        _bytes = new byte[chunkFrames * 2 * sizeof(float)];
    }

    /// <summary>Frames in each chunk.</summary>
    public int ChunkFrames => _chunkFrames;

    /// <summary>Makes the next render hold the thread for this long first, to prove a stall shows up as a dropout.</summary>
    public void StallNext(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        _stallMilliseconds = milliseconds;
    }

    /// <summary>Renders the next chunk. The returned array is reused: copy it before the next call.</summary>
    public byte[] Render()
    {
        if (_stallMilliseconds > 0)
        {
            long until = Stopwatch.GetTimestamp() + (_stallMilliseconds * Stopwatch.Frequency / 1000);
            _stallMilliseconds = 0;
            while (Stopwatch.GetTimestamp() < until)
            {
            }
        }
        Span<float> samples = MemoryMarshal.Cast<byte, float>(_bytes.AsSpan());
        _engine.Render(samples[.._chunkFrames], samples[_chunkFrames..]);
        return _bytes;
    }
}
