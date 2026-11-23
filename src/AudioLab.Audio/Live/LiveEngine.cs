using System.Diagnostics;
using AudioLab.Core.Playback;

namespace AudioLab.Audio.Live;

/// <summary>Plays a render plan live: the transport, the loop, the click and the swap of a plan for the next one, one chunk at a time.</summary>
/// <remarks>
/// Playing does not stop at the end of the content, as in most workstations; only a stop or a pause ends it. One caller at a time.
/// <see cref="Render"/> allocates nothing and takes no lock; every other member is called between two chunks.
/// Pausing and stopping fade out over a few milliseconds, as starting fades in, so a transport change does not click. A new plan is
/// started cold at the playhead (D35: its effects start empty, so a reverb or delay tail of the old plan is cut) and crossfaded over
/// one chunk with the old one, which hides the step but not the missing tail.
/// </remarks>
public sealed class LiveEngine
{
    private const int FadeFrames = 256;

    // The ramp either side of a loop wrap, which is a jump in the music: about a millisecond, enough to take the click out.
    private const int WrapFrames = 64;

    private readonly int _sampleRate;
    private readonly float[] _swapLeft;
    private readonly float[] _swapRight;
    private MixerGraph? _graph;
    private MixerGraph? _next;
    private RenderPlan? _graphPlan;
    private RenderPlan? _nextPlan;
    private ClickGenerator? _click;
    private ClickGenerator? _nextClick;
    private long _nextRevision;
    private bool _clickOn;
    private bool _fadeIn;
    private bool _ending;
    private bool _endToCue;
    private float[] _peaks = [];
    private long _jump = -1;
    private int _rampIn;
    private bool _loop;
    private long _loopStart;
    private long _loopEnd;
    private long _chunks;
    private long _overruns;
    private double _maxMilliseconds;

    /// <summary>Creates an engine whose chunks are at most <paramref name="maxChunkFrames"/> frames.</summary>
    public LiveEngine(int sampleRate, int maxChunkFrames)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, AudioFormat.MinSampleRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, AudioFormat.MaxSampleRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChunkFrames, 1);
        _sampleRate = sampleRate;
        _swapLeft = new float[maxChunkFrames];
        _swapRight = new float[maxChunkFrames];
    }

    /// <summary>Whether the last chunk was audio the listener is due (playing with a plan), not the silence of a stop, a pause or no plan.</summary>
    public bool Audible { get; private set; }

    /// <summary>Whether the engine is playing.</summary>
    public TransportState State { get; private set; }

    /// <summary>The timeline frame the next chunk starts on.</summary>
    public long Position { get; private set; }

    /// <summary>The revision of the document the plan in use was made from, or -1 before the first plan.</summary>
    public long Revision { get; private set; } = -1;

    /// <summary>The frame a stop returns to: where playing last began.</summary>
    public long Cue { get; private set; }

    /// <summary>Whether the loop is on.</summary>
    public bool Loop => _loop;

    /// <summary>Hands the engine a plan to play from the next chunk on, and the click for the same document.</summary>
    /// <remarks>A plan not yet played when a newer one arrives is dropped. While playing the swap is crossfaded, otherwise immediate.</remarks>
    public void Install(RenderPlan plan, long revision, ClickGenerator? click)
    {
        ArgumentNullException.ThrowIfNull(plan);
        MixerGraph graph = new(plan);
        graph.Seek(Position);
        if (State == TransportState.Playing && _graph is not null)
        {
            _next = graph;
            _nextPlan = plan;
            _nextRevision = revision;
            _nextClick = click;
            return;
        }
        _graph = graph;
        _graphPlan = plan;
        _next = null;
        _click = click;
        Revision = revision;
    }

    /// <summary>Starts playing from the playhead; the playhead becomes the frame a stop returns to.</summary>
    public void Play()
    {
        if (State == TransportState.Playing && !_ending)
        {
            return;
        }
        if (State != TransportState.Playing)
        {
            Cue = Position;
            _fadeIn = true;
        }
        _ending = false;
        State = TransportState.Playing;
    }

    /// <summary>Stops playing after a short fade, and leaves the playhead where it stopped.</summary>
    public void Pause()
    {
        if (State != TransportState.Playing)
        {
            return;
        }
        if (_graph is null)
        {
            // Nothing is being rendered, so there is nothing to fade and no chunk that would carry the change out.
            State = TransportState.Paused;
            _ending = false;
            return;
        }
        _ending = true;
        _endToCue = false;
    }

    /// <summary>Stops playing after a short fade, and returns the playhead to where playing began.</summary>
    public void Stop()
    {
        if (State == TransportState.Playing && _graph is not null)
        {
            _ending = true;
            _endToCue = true;
            return;
        }
        State = TransportState.Stopped;
        _ending = false;
        _jump = -1;
        MoveTo(Cue);
    }

    /// <summary>Moves the playhead to a timeline frame, clearing every effect, as a jump does (D35).</summary>
    public void Seek(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        if (State == TransportState.Playing && _graph is not null)
        {
            // A jump while playing fades the chunk it interrupts out and the next one in, so it does not click.
            _jump = frame;
            return;
        }
        MoveTo(frame);
        if (State != TransportState.Playing)
        {
            Cue = frame;
        }
    }

    /// <summary>Turns the loop on or off; playing wraps from <paramref name="end"/> back to <paramref name="start"/>.</summary>
    public void SetLoop(bool on, long start, long end)
    {
        _loop = on && start >= 0 && end - start >= FadeFrames;
        _loopStart = start;
        _loopEnd = end;
    }

    /// <summary>Turns the metronome on or off.</summary>
    public void SetClick(bool on) => _clickOn = on;

    /// <summary>Reads and clears the peak levels of the plan being played; empty before the first plan. The next reading reuses the array.</summary>
    public MeterReading TakeLevels()
    {
        if (_graph is not { } graph || _graphPlan is not { } plan)
        {
            return new MeterReading([], []);
        }
        if (_peaks.Length != graph.PeakCount)
        {
            _peaks = new float[graph.PeakCount];
        }
        graph.TakePeaks(_peaks);
        return new MeterReading(plan.TrackIds, _peaks);
    }

    /// <summary>Reads and clears the render timings.</summary>
    public RenderStats TakeStats()
    {
        RenderStats stats = new(_chunks, _overruns, _maxMilliseconds);
        _maxMilliseconds = 0;
        return stats;
    }

    /// <summary>Renders the next chunk into two equally long buffers, no longer than the engine's largest chunk.</summary>
    public void Render(Span<float> left, Span<float> right)
    {
        if (left.Length != right.Length || left.Length > _swapLeft.Length)
        {
            throw new ArgumentException("The buffers must be of one length, no longer than the engine's largest chunk.", nameof(right));
        }
        long started = Stopwatch.GetTimestamp();
        Audible = State == TransportState.Playing && _graph is not null;
        left.Clear();
        right.Clear();
        if (State == TransportState.Playing && _graph is not null)
        {
            RenderPlaying(left, right);
        }
        double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        _chunks++;
        _maxMilliseconds = Math.Max(_maxMilliseconds, milliseconds);
        if (milliseconds > left.Length * 1000.0 / _sampleRate)
        {
            _overruns++;
        }
    }

    private void MoveTo(long frame)
    {
        Position = frame;
        _graph?.Seek(frame);
        _next?.Seek(frame);
    }

    private void RenderPlaying(Span<float> left, Span<float> right)
    {
        long from = Position;
        if (_next is { } next)
        {
            MixerGraph old = _graph!;
            Span<float> swapLeft = _swapLeft.AsSpan(0, left.Length);
            Span<float> swapRight = _swapRight.AsSpan(0, left.Length);
            Position = RenderThrough(old, from, left, right);
            RenderThrough(next, from, swapLeft, swapRight);
            for (int i = 0; i < left.Length; i++)
            {
                float blend = (i + 1f) / left.Length;
                left[i] += (swapLeft[i] - left[i]) * blend;
                right[i] += (swapRight[i] - right[i]) * blend;
            }
            _graph = next;
            _graphPlan = _nextPlan;
            _next = null;
            _click = _nextClick;
            Revision = _nextRevision;
        }
        else
        {
            Position = RenderThrough(_graph!, from, left, right);
        }
        if (_clickOn && _click is not null)
        {
            AddClick(from, left, right);
        }
        FadeWraps(from, left, right);
        ApplyFades(left, right);
        if (_ending)
        {
            State = _endToCue ? TransportState.Stopped : TransportState.Paused;
            _ending = false;
            _jump = -1;
            if (_endToCue)
            {
                MoveTo(Cue);
            }
        }
        else if (_jump >= 0)
        {
            MoveTo(_jump);
            _jump = -1;
            _fadeIn = true;
        }
    }

    // Frames of one graph from a position, wrapping at the loop's end; the graph is where the position is.
    private long RenderThrough(MixerGraph graph, long from, Span<float> left, Span<float> right)
    {
        long position = from;
        int done = 0;
        while (done < left.Length)
        {
            int count = SegmentLength(position, left.Length - done);
            graph.Render(left.Slice(done, count), right.Slice(done, count));
            long next = Advance(position, count);
            if (next != position + count)
            {
                graph.Seek(next);
            }
            position = next;
            done += count;
        }
        return position;
    }

    private void AddClick(long from, Span<float> left, Span<float> right)
    {
        long position = from;
        int done = 0;
        while (done < left.Length)
        {
            int count = SegmentLength(position, left.Length - done);
            _click!.Mix(position, left.Slice(done, count), right.Slice(done, count));
            position = Advance(position, count);
            done += count;
        }
    }

    // Ramps down into each wrap and up out of it; a ramp up that runs past the end of the chunk goes on in the next.
    private void FadeWraps(long from, Span<float> left, Span<float> right)
    {
        int carried = Math.Min(_rampIn, left.Length);
        for (int i = 0; i < carried; i++)
        {
            float gain = (WrapFrames - _rampIn + i + 1f) / WrapFrames;
            left[i] *= gain;
            right[i] *= gain;
        }
        _rampIn = Math.Max(0, _rampIn - left.Length);
        if (!_loop)
        {
            return;
        }
        long position = from;
        int done = 0;
        while (done < left.Length)
        {
            int count = SegmentLength(position, left.Length - done);
            long next = Advance(position, count);
            done += count;
            if (next != position + count)
            {
                RampAt(done, left, right);
            }
            position = next;
        }
    }

    private void RampAt(int wrap, Span<float> left, Span<float> right)
    {
        int down = Math.Min(WrapFrames, wrap);
        for (int i = 0; i < down; i++)
        {
            float gain = 1f - ((i + 1f) / down);
            left[wrap - down + i] *= gain;
            right[wrap - down + i] *= gain;
        }
        int up = Math.Min(WrapFrames, left.Length - wrap);
        for (int i = 0; i < up; i++)
        {
            float gain = (i + 1f) / WrapFrames;
            left[wrap + i] *= gain;
            right[wrap + i] *= gain;
        }
        if (up < WrapFrames)
        {
            _rampIn = WrapFrames - up;
        }
    }

    // A playhead already past the loop's end plays on; only one that reaches the end exactly wraps.
    private int SegmentLength(long position, int remaining) =>
        _loop && position < _loopEnd ? (int)Math.Min(remaining, _loopEnd - position) : remaining;

    private long Advance(long position, int count)
    {
        long next = position + count;
        return _loop && next == _loopEnd ? _loopStart : next;
    }

    private void ApplyFades(Span<float> left, Span<float> right)
    {
        int length = Math.Min(FadeFrames, left.Length);
        if (_fadeIn)
        {
            for (int i = 0; i < length; i++)
            {
                float gain = (i + 1f) / length;
                left[i] *= gain;
                right[i] *= gain;
            }
            _fadeIn = false;
        }
        if (_ending || _jump >= 0)
        {
            int first = left.Length - length;
            for (int i = 0; i < length; i++)
            {
                float gain = 1f - ((i + 1f) / length);
                left[first + i] *= gain;
                right[first + i] *= gain;
            }
        }
    }
}
