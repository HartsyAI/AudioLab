using System.Diagnostics;

namespace AudioLab.Audio;

/// <summary>The mixer graph: tracks into a master with one reverb send, rendered a block at a time with no allocation.</summary>
public sealed class AudioEngine
{
    private readonly List<Track> _tracks = [];

    private readonly Reverb _reverb;

    private readonly int _blockFrames;

    private readonly float[] _mixLeft;

    private readonly float[] _mixRight;

    private readonly float[] _send;

    private double _seconds;

    /// <summary>Creates an engine that works through the audio in blocks of at most <paramref name="blockFrames" /> frames.</summary>
    public AudioEngine(int sampleRate, int blockFrames)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockFrames);
        SampleRate = sampleRate;
        _blockFrames = blockFrames;
        _mixLeft = new float[blockFrames];
        _mixRight = new float[blockFrames];
        _send = new float[blockFrames];
        _reverb = new Reverb(sampleRate);
        Load = new RenderLoad(sampleRate);
    }

    /// <summary>Frames per second.</summary>
    public int SampleRate { get; }

    /// <summary>The master fader, 1 for unity.</summary>
    public float MasterGain { get; set; } = 0.2f;

    /// <summary>How rendering time compares with the audio it made.</summary>
    public RenderLoad Load { get; }

    /// <summary>The dropouts the output reports back; the engine only keeps the count.</summary>
    public DropoutCounter Dropouts { get; } = new();

    /// <summary>The number of tracks.</summary>
    public int TrackCount => _tracks.Count;

    /// <summary>Adds a track; call before rendering starts, not from the audio path.</summary>
    public Track AddTrack(ITrackSource source, float cutoff)
    {
        Track track = new(source, SampleRate, _blockFrames, cutoff);
        _tracks.Add(track);
        return track;
    }

    /// <summary>Renders the next frames, as many as the buffers hold, which have the same length.</summary>
    public void Render(Span<float> left, Span<float> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("The two channels must have the same length.", nameof(right));
        }
        long started = Stopwatch.GetTimestamp();
        for (int i = 0; i < _tracks.Count; i++)
        {
            _tracks[i].Modulate(_seconds);
        }
        for (int offset = 0; offset < left.Length; offset += _blockFrames)
        {
            int frames = Math.Min(_blockFrames, left.Length - offset);
            RenderBlock(frames, left.Slice(offset, frames), right.Slice(offset, frames));
        }
        _seconds += (double)left.Length / SampleRate;
        Load.Record(Stopwatch.GetTimestamp() - started, left.Length);
    }

    private void RenderBlock(int frames, Span<float> left, Span<float> right)
    {
        Span<float> mixLeft = _mixLeft.AsSpan(0, frames);
        Span<float> mixRight = _mixRight.AsSpan(0, frames);
        Span<float> send = _send.AsSpan(0, frames);
        mixLeft.Clear();
        mixRight.Clear();
        send.Clear();
        for (int i = 0; i < _tracks.Count; i++)
        {
            _tracks[i].Mix(frames, mixLeft, mixRight, send);
        }
        _reverb.Process(send, mixLeft, mixRight);
        for (int i = 0; i < frames; i++)
        {
            float l = mixLeft[i] * MasterGain;
            float r = mixRight[i] * MasterGain;
            left[i] = l / (1 + MathF.Abs(l));
            right[i] = r / (1 + MathF.Abs(r));
        }
    }
}
