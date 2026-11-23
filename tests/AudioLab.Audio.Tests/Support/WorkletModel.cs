using System.Buffers.Binary;
using System.Runtime.InteropServices;
using AudioLab.Audio.Live;

namespace AudioLab.Audio.Tests.Support;

/// <summary>A C# model of the ring, the credits and the dropout counter in the AudioWorklet half of <c>audio-shim.js</c>, line for line.</summary>
/// <remarks>The shim cannot run in a test, so this holds its rules where a test can drive them: change one and the other must change.</remarks>
public sealed class WorkletModel(int chunk, int slots)
{
    private readonly int _capacity = chunk * slots;
    private readonly float[] _left = new float[chunk * slots];
    private readonly double[] _starts = new double[slots];
    private readonly bool[] _live = new bool[slots];
    private int _written;
    private int _read;
    private int _consumed;
    private bool _inRun;
    private bool _wasLive;

    /// <summary>Frames waiting in the ring.</summary>
    public int Fill { get; private set; }

    /// <summary>Whether the ring has filled once.</summary>
    public bool Primed { get; private set; }

    /// <summary>Runs of quanta that found too little audio while audio was due.</summary>
    public long Events { get; private set; }

    /// <summary>Frames of silence played in place of due audio.</summary>
    public long Missed { get; private set; }

    /// <summary>The credits the worker has not answered yet.</summary>
    public int Credits { get; private set; }

    /// <summary>The timeline frame of the next frame to play.</summary>
    public double Position => _starts[_read / chunk] + (_read % chunk);

    /// <summary>The credits sent when the port connects: one for each slot.</summary>
    public void Connect() => Credits += slots;

    /// <summary>Takes one chunk as the worker sends it.</summary>
    public void Push(byte[] bytes)
    {
        Credits--;
        int at = _written % slots * chunk;
        ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(bytes.AsSpan(LiveHost.HeaderBytes));
        samples[..chunk].CopyTo(_left.AsSpan(at, chunk));
        _starts[_written % slots] = BinaryPrimitives.ReadDoubleLittleEndian(bytes);
        _live[_written % slots] = BinaryPrimitives.ReadDoubleLittleEndian(bytes.AsSpan(8)) == 1;
        _written++;
        Fill += chunk;
        if (Fill > _capacity)
        {
            throw new InvalidOperationException("The ring overflowed: more chunks were asked for than it holds.");
        }
        if (!Primed && Fill >= _capacity)
        {
            Primed = true;
        }
    }

    /// <summary>Plays one quantum and returns how many chunks it asks the worker for.</summary>
    public int Process(int quantum)
    {
        int credits = 0;
        if (!Primed)
        {
            return credits;
        }
        int got = Math.Min(Fill, quantum);
        if (got > 0)
        {
            _wasLive = _live[_read / chunk];
        }
        _read = (_read + got) % _capacity;
        Fill -= got;
        _consumed += got;
        if (got < quantum && _wasLive)
        {
            Missed += quantum - got;
            if (!_inRun)
            {
                Events++;
                _inRun = true;
            }
        }
        else
        {
            _inRun = false;
        }
        while (_consumed >= chunk)
        {
            _consumed -= chunk;
            credits++;
        }
        Credits += credits;
        return credits;
    }
}
