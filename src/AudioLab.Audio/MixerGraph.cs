using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio;

/// <summary>The mixer: pulls a prepared plan through clips, inserts, faders, sends, buses and the master, block by block (D5).</summary>
/// <remarks>
/// <see cref="Render"/> allocates nothing and takes no lock. Every time-dependent value comes from the absolute frame, so
/// the output does not depend on how the caller cuts it into blocks. One caller at a time.
/// </remarks>
public sealed class MixerGraph
{
    /// <summary>The largest block the graph works on at once; a longer request is cut into several.</summary>
    public const int DefaultBlockFrames = 512;

    // How often automation lanes are read, in frames: 0.7 ms at 48 kHz.
    private const int ControlFrames = 32;

    private readonly RenderPlan _plan;
    private readonly MasterLimiter _limiter;
    private readonly int _blockFrames;
    private readonly float[] _trackLeft;
    private readonly float[] _trackRight;
    private readonly float[][] _busLeft;
    private readonly float[][] _busRight;

    /// <summary>Creates a graph that renders <paramref name="plan"/> from the start of the project.</summary>
    public MixerGraph(RenderPlan plan, int blockFrames = DefaultBlockFrames)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfLessThan(blockFrames, 1);
        _plan = plan;
        _blockFrames = blockFrames;
        _limiter = new MasterLimiter(plan.Format.SampleRate);
        _trackLeft = new float[blockFrames];
        _trackRight = new float[blockFrames];
        _busLeft = new float[plan.BusCount][];
        _busRight = new float[plan.BusCount][];
        for (int bus = 0; bus < plan.BusCount; bus++)
        {
            _busLeft[bus] = new float[blockFrames];
            _busRight[bus] = new float[blockFrames];
        }
    }

    /// <summary>The timeline frame the next block starts on.</summary>
    public long Position { get; private set; }

    /// <summary>The format of the blocks: stereo at the plan's sample rate.</summary>
    public AudioFormat Format => _plan.Format;

    /// <summary>Moves to a timeline frame and clears every effect, voice and the limiter, as a jump of the playhead does.</summary>
    public void Seek(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        Position = frame;
        _limiter.Reset();
        foreach (PlannedTrack track in _plan.Tracks)
        {
            foreach (IInsert insert in track.Inserts)
            {
                insert.Reset();
            }
            track.Source?.Reset();
        }
    }

    /// <summary>Renders the next frames of the mix into two equally long buffers, replacing what they hold, and moves the position on.</summary>
    public void Render(Span<float> left, Span<float> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("The left and right buffers must have the same length.", nameof(right));
        }
        int done = 0;
        while (done < left.Length)
        {
            int count = Math.Min(_blockFrames, left.Length - done);
            RenderBlock(left.Slice(done, count), right.Slice(done, count));
            done += count;
        }
    }

    private void RenderBlock(Span<float> left, Span<float> right)
    {
        int count = left.Length;
        left.Clear();
        right.Clear();
        for (int bus = 0; bus < _busLeft.Length; bus++)
        {
            _busLeft[bus].AsSpan(0, count).Clear();
            _busRight[bus].AsSpan(0, count).Clear();
        }
        foreach (PlannedTrack track in _plan.Tracks)
        {
            RenderTrack(track, left, right);
        }
        float master = _plan.MasterGain;
        for (int i = 0; i < count; i++)
        {
            left[i] *= master;
            right[i] *= master;
        }
        if (_plan.Limiter)
        {
            _limiter.Process(left, right);
        }
        Position += count;
    }

    private void RenderTrack(PlannedTrack track, Span<float> outLeft, Span<float> outRight)
    {
        int count = outLeft.Length;
        Span<float> left;
        Span<float> right;
        if (track.IsBus)
        {
            left = _busLeft[track.BusSlot].AsSpan(0, count);
            right = _busRight[track.BusSlot].AsSpan(0, count);
        }
        else
        {
            left = _trackLeft.AsSpan(0, count);
            right = _trackRight.AsSpan(0, count);
            left.Clear();
            right.Clear();
            MixClips(track, Position, left, right);
            track.Source?.Render(Position, left, right);
        }
        foreach (IInsert insert in track.Inserts)
        {
            insert.Process(left, right, Position);
        }
        SendInto(track, pre: true, left, right);
        FaderAndPan(track, left, right);
        SendInto(track, pre: false, left, right);
        if (track.SoloAudible)
        {
            for (int i = 0; i < count; i++)
            {
                outLeft[i] += left[i];
                outRight[i] += right[i];
            }
        }
    }

    // A block finds its first clip by searching the running maximum of the clip ends, then walks on while clips still start before it ends.
    private static void MixClips(PlannedTrack track, long blockStart, Span<float> left, Span<float> right)
    {
        PlannedClip[] clips = track.Clips;
        long blockEnd = blockStart + left.Length;
        int low = 0;
        int high = clips.Length;
        while (low < high)
        {
            int middle = (low + high) >>> 1;
            if (track.LatestEnds[middle] > blockStart)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }
        for (int i = low; i < clips.Length && clips[i].SpanStart < blockEnd; i++)
        {
            clips[i].MixInto(blockStart, left, right);
        }
    }

    private void SendInto(PlannedTrack track, bool pre, ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        foreach (PlannedSend send in track.Sends)
        {
            if (send.Pre != pre)
            {
                continue;
            }
            Span<float> busLeft = _busLeft[send.BusSlot];
            Span<float> busRight = _busRight[send.BusSlot];
            for (int i = 0; i < left.Length; i++)
            {
                busLeft[i] += left[i] * send.Gain;
                busRight[i] += right[i] * send.Gain;
            }
        }
    }

    private void FaderAndPan(PlannedTrack track, Span<float> left, Span<float> right)
    {
        if (track.VolumeLane is null && track.PanLane is null)
        {
            StereoPan staticPan = new(track.Pan, track.Mono);
            for (int i = 0; i < left.Length; i++)
            {
                (left[i], right[i]) = staticPan.Apply(left[i] * track.FaderGain, right[i] * track.FaderGain);
            }
            return;
        }
        // The lanes are read on a grid of whole frames, not at every sample: the volume is straight between grid points and the pan holds
        // one value per cell. The grid is fixed to the timeline, so the output does not depend on where a block starts.
        int rate = _plan.Format.SampleRate;
        int done = 0;
        while (done < left.Length)
        {
            long frame = Position + done;
            long cell = frame - (frame % ControlFrames);
            int count = (int)Math.Min(left.Length - done, cell + ControlFrames - frame);
            double from = track.VolumeLane is null ? 1 : LaneValue(track.VolumeLane, cell, rate);
            double to = track.VolumeLane is null ? 1 : LaneValue(track.VolumeLane, cell + ControlFrames, rate);
            double pan = track.Pan;
            if (track.PanLane is not null)
            {
                pan += LaneValue(track.PanLane, cell, rate);
            }
            StereoPan cellPan = new(pan, track.Mono);
            for (int i = done; i < done + count; i++)
            {
                double volume = from + ((to - from) * (Position + i - cell) / ControlFrames);
                float gain = track.FaderGain * (float)Math.Max(0, volume);
                (left[i], right[i]) = cellPan.Apply(left[i] * gain, right[i] * gain);
            }
            done += count;
        }
    }

    // The plan keeps only lanes that have points, so a lane always has a value.
    private static double LaneValue(ValueList<AutomationPoint> lane, long frame, int rate) =>
        AutomationRules.ValueAt(lane, (double)frame / rate)!.Value;
}
