using AudioLab.Core.Model;
using AudioLab.Core.Playback;

namespace AudioLab.Web.Session;

/// <summary>Records a pass of one track's automation while the transport plays, and commits it when playing stops (D72, D73).</summary>
/// <remarks>
/// A pass begins when the transport starts playing with a lane in a recording mode. It ends when the transport stops or pauses, and that commits
/// it; a drop (a lost connection, a project switch, Escape) ends it without committing. The samples are the transport's own ticks, so there is no
/// second clock. Playing again after a drop starts a pass only once the transport has left the playing state.
/// </remarks>
public sealed class AutomationRecorder : IDisposable
{
    private IPlaybackTransport? _transport;

    private Func<AutomationTarget?>? _target;

    private Func<TrackId, AutomationParameter, AutomationPass, AutomationMode?, Task>? _commit;

    private AutomationWriter? _writer;

    private AutomationTarget? _pass;

    private TransportState _last = TransportState.Stopped;

    private double _time;

    private double _value;

    /// <summary>Whether a pass is recording now.</summary>
    public bool Passing => _writer is { Passing: true };

    /// <summary>The commit of the last pass that stopped; the transport does not wait for it, and a test may.</summary>
    public Task Committing { get; private set; } = Task.CompletedTask;

    /// <summary>Starts following a transport: the target names the lane a new pass writes, and commit receives a finished pass.</summary>
    /// <param name="transport">The transport whose ticks are the samples.</param>
    /// <param name="target">The lane in a recording mode, or null; read when playing begins.</param>
    /// <param name="commit">Commits a pass with the mode to set after it; the same path every edit takes.</param>
    public void Open(
        IPlaybackTransport transport,
        Func<AutomationTarget?> target,
        Func<TrackId, AutomationParameter, AutomationPass, AutomationMode?, Task> commit)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(commit);
        Close();
        _transport = transport;
        _target = target;
        _commit = commit;
        _last = transport.Transport;
        transport.Changed += OnTransport;
    }

    /// <summary>Stops following the transport and drops any pass, which commits nothing.</summary>
    public void Close()
    {
        Drop();
        if (_transport is not null)
        {
            _transport.Changed -= OnTransport;
        }
        _transport = null;
        _target = null;
        _commit = null;
    }

    /// <summary>Whether a lane's point drag is a touch or latch segment of the pass that is recording.</summary>
    /// <param name="track">The track whose lane is dragged.</param>
    public bool RecordsLane(TrackId track) =>
        Passing && _pass is { } pass && pass.Track == track && pass.Mode is AutomationMode.Touch or AutomationMode.Latch;

    /// <summary>The control is grabbed at the playhead with a value: a touch or latch segment starts.</summary>
    /// <param name="track">The track whose lane is grabbed.</param>
    /// <param name="value">The value the control holds.</param>
    public void Grab(TrackId track, double value)
    {
        if (!RecordsLane(track))
        {
            return;
        }
        _value = value;
        _time = _transport!.Position;
        _writer!.Grab(_time, value);
    }

    /// <summary>The held control moved to a value; the next sample and the release record it.</summary>
    /// <param name="track">The track whose lane is held.</param>
    /// <param name="value">The value the control holds now.</param>
    public void Move(TrackId track, double value)
    {
        if (RecordsLane(track))
        {
            _value = value;
        }
    }

    /// <summary>The control is let go at the playhead with a value: the segment ends, and a latch keeps this value.</summary>
    /// <param name="track">The track whose lane is released.</param>
    /// <param name="value">The value the control holds on release.</param>
    public void Release(TrackId track, double value)
    {
        if (!RecordsLane(track))
        {
            return;
        }
        _value = value;
        _time = _transport!.Position;
        _writer!.Release(_time, value);
    }

    /// <summary>Drops the pass that is recording, if any; nothing is committed and the transport keeps playing.</summary>
    public void Drop()
    {
        _writer = null;
        _pass = null;
    }

    /// <inheritdoc />
    public void Dispose() => Close();

    private void OnTransport()
    {
        if (_transport is null)
        {
            return;
        }
        TransportState now = _transport.Transport;
        bool began = now == TransportState.Playing && _last != TransportState.Playing;
        _last = now;
        if (Passing && now != TransportState.Playing)
        {
            Finish();
            return;
        }
        if (Passing)
        {
            _time = _transport.Position;
            _writer!.Sample(_time, Current());
            return;
        }
        if (began && _target?.Invoke() is { } target && target.Mode is AutomationMode.Write or AutomationMode.Touch or AutomationMode.Latch)
        {
            Begin(target);
        }
    }

    private void Begin(AutomationTarget target)
    {
        _pass = target;
        _writer = new AutomationWriter(target.Mode, target.Parameter);
        _value = target.Resting();
        _time = _transport!.Position;
        _writer.Begin(_time, _value);
    }

    // A write pass records the control as it rests; touch and latch record the value the user holds, which a latch keeps after the release.
    private double Current() => _pass is { Mode: AutomationMode.Write } write ? write.Resting() : _value;

    private void Finish()
    {
        AutomationWriter writer = _writer!;
        AutomationTarget target = _pass!.Value;
        double last = Current();
        Drop();
        AutomationPass? pass = writer.Stop(_time, last);
        if (pass is not { } committed)
        {
            return;
        }
        // A write pass ends by handing the lane to touch, in the same gesture as its commits (D25).
        AutomationMode? after = target.Mode == AutomationMode.Write ? AutomationMode.Touch : null;
        Committing = _commit!(target.Track, target.Parameter, committed, after);
    }
}
