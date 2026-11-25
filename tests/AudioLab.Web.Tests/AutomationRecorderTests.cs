using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The recorder on a fake transport and a fake hub: what a pass records, what it commits, and what a drop leaves (D72, D73).</summary>
public sealed class AutomationRecorderTests
{
    private readonly FakePlaybackTransport _transport = new();

    private readonly FakeProjectHub _hub = new()
    {
        JoinDocument = ProjectJson.Write(Song.Empty().With(Song.NewTrack("t1"))),
    };

    private readonly List<(AutomationPass Pass, AutomationParameter Parameter, AutomationMode? Mode)> _commits = [];

    private double _resting = 0.4;

    private static readonly TrackId _track = TrackId.From("t1");

    private AutomationRecorder Open(AutomationMode mode, AutomationParameter parameter = AutomationParameter.Volume)
    {
        AutomationRecorder recorder = new();
        recorder.Open(
            _transport,
            () => new AutomationTarget(_track, parameter, mode, () => _resting),
            (_, commitParameter, pass, after) =>
            {
                _commits.Add((pass, commitParameter, after));
                return Task.CompletedTask;
            });
        return recorder;
    }

    private async Task<LiveProject> OpenHubAsync()
    {
        LiveProject live = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        _hub.Calls.Clear();
        _hub.Reply = new CommandReply(true, true, 2, [], []);
        return live;
    }

    /// <summary>A write pass recorded across ticks is one segment, and its commit and mode change are one gesture on the hub.</summary>
    [Fact]
    public async Task AWritePassRecordedAcrossTicksCommitsOneGesture()
    {
        await using LiveProject live = await OpenHubAsync();
        AutomationEditor editor = new(live);
        AutomationRecorder recorder = new();
        recorder.Open(
            _transport,
            () => new AutomationTarget(_track, AutomationParameter.Volume, AutomationMode.Write, () => _resting),
            (track, parameter, pass, after) => editor.CommitPassAsync(track, parameter, pass, after));

        _transport.Tick(TransportState.Playing, 0);
        for (int tick = 1; tick <= 10; tick++)
        {
            if (tick == 5)
            {
                _resting = 0.7;
            }
            _transport.Tick(TransportState.Playing, tick * 0.1);
        }
        _transport.Tick(TransportState.Stopped, 1.0);
        await recorder.Committing;

        Assert.Equal(["automation_write_commit", "automation_set_mode"], _hub.Sent.Select(sent => sent.Command));
        string? gesture = _hub.Sent[0].Gesture;
        Assert.NotNull(gesture);
        Assert.All(_hub.Sent, sent => Assert.Equal(gesture, sent.Gesture));
    }

    /// <summary>A write pass keeps every sample in time order and ends by handing the lane to touch.</summary>
    [Fact]
    public void AWritePassSamplesEveryTickAndHandsTheLaneToTouch()
    {
        AutomationRecorder recorder = Open(AutomationMode.Write);

        _transport.Tick(TransportState.Playing, 0);
        for (int tick = 1; tick <= 10; tick++)
        {
            _transport.Tick(TransportState.Playing, tick * 0.1);
        }
        _transport.Tick(TransportState.Stopped, 1.0);

        Assert.False(recorder.Passing);
        (AutomationPass pass, _, AutomationMode? after) = Assert.Single(_commits);
        IReadOnlyList<WrittenPoint> points = Assert.Single(pass.Segments);
        Assert.Equal(11, points.Count);
        Assert.All(points.Zip(points.Skip(1)), pair => Assert.True(pair.Second.Time > pair.First.Time));
        Assert.False(pass.ReturnToOld);
        Assert.Equal(AutomationMode.Touch, after);
    }

    /// <summary>A touch pass records only the grab-to-release span, so the old curve outside it is kept.</summary>
    [Fact]
    public void ATouchPassKeepsTheCurveOutsideTheTouchedSpan()
    {
        AutomationRecorder recorder = Open(AutomationMode.Touch);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 1.0);

        recorder.Grab(_track, 0.9);
        recorder.Move(_track, 0.9);
        _transport.Tick(TransportState.Playing, 1.5);
        recorder.Release(_track, 0.9);
        _transport.Tick(TransportState.Playing, 2.0);
        _transport.Tick(TransportState.Playing, 3.0);
        _transport.Tick(TransportState.Stopped, 3.0);

        (AutomationPass pass, _, AutomationMode? after) = Assert.Single(_commits);
        IReadOnlyList<WrittenPoint> points = Assert.Single(pass.Segments);
        Assert.True(pass.ReturnToOld);
        Assert.Null(after);
        Assert.All(points, point => Assert.InRange(point.Time, 1.0, 1.5));
        Assert.All(points, point => Assert.Equal(0.9, point.Value, 6));
    }

    /// <summary>A latch pass keeps the value the control held at its release, after the control has let go.</summary>
    [Fact]
    public void ALatchPassHoldsTheLastValue()
    {
        AutomationRecorder recorder = Open(AutomationMode.Latch);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 1.0);

        recorder.Grab(_track, 0.6);
        _transport.Tick(TransportState.Playing, 1.2);
        recorder.Move(_track, 0.7);
        _transport.Tick(TransportState.Playing, 1.4);
        recorder.Release(_track, 0.7);
        _resting = 0.2;
        _transport.Tick(TransportState.Playing, 2.0);
        _transport.Tick(TransportState.Playing, 3.0);
        _transport.Tick(TransportState.Stopped, 3.0);

        (AutomationPass pass, _, AutomationMode? after) = Assert.Single(_commits);
        IReadOnlyList<WrittenPoint> points = Assert.Single(pass.Segments);
        Assert.False(pass.ReturnToOld);
        Assert.Null(after);
        WrittenPoint last = points[^1];
        Assert.Equal(3.0, last.Time, 6);
        Assert.Equal(0.7, last.Value, 6);
        Assert.Equal(0.7, points.First(point => point.Time >= 2.0).Value, 6);
    }

    /// <summary>A pass that is recording when the connection is lost is dropped: nothing is committed.</summary>
    [Fact]
    public void ADisconnectDropsThePassWithoutCommitting()
    {
        AutomationRecorder recorder = Open(AutomationMode.Write);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);

        recorder.Drop();
        _transport.Tick(TransportState.Stopped, 1.0);

        Assert.False(recorder.Passing);
        Assert.Empty(_commits);
    }

    /// <summary>Closing the recorder, which a project switch does, drops the pass and stops the transport from reaching it.</summary>
    [Fact]
    public void AProjectSwitchDropsThePassWithoutCommitting()
    {
        AutomationRecorder recorder = Open(AutomationMode.Write);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);

        recorder.Close();
        _transport.Tick(TransportState.Stopped, 1.0);

        Assert.False(recorder.Passing);
        Assert.Empty(_commits);
    }

    /// <summary>Escape drops the pass, and a pass starts again only after the transport has been away from playing.</summary>
    [Fact]
    public void AnEscapeDropsThePassAndNextPlayStartsANewOne()
    {
        AutomationRecorder recorder = Open(AutomationMode.Write);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);

        recorder.Drop();
        _transport.Tick(TransportState.Playing, 0.6);
        Assert.False(recorder.Passing);
        _transport.Tick(TransportState.Stopped, 0.7);
        Assert.Empty(_commits);

        _transport.Tick(TransportState.Playing, 0);
        Assert.True(recorder.Passing);
        _transport.Tick(TransportState.Playing, 0.4);
        _transport.Tick(TransportState.Stopped, 0.4);

        (AutomationPass pass, _, _) = Assert.Single(_commits);
        Assert.Equal(0, pass.Segments[0][0].Time, 6);
    }

    /// <summary>A refused commit changes nothing: the first segment is refused, so no mode change is sent and nothing says it landed.</summary>
    [Fact]
    public async Task ARefusedCommitChangesNothing()
    {
        await using LiveProject live = await OpenHubAsync();
        _hub.ReplyForApply = count => count == 1
            ? FakeProjectHub.Refused(CommandErrorCode.Rejected, "The lane is full.")
            : null;
        AutomationEditor editor = new(live);
        AutomationRecorder recorder = new();
        recorder.Open(
            _transport,
            () => new AutomationTarget(_track, AutomationParameter.Volume, AutomationMode.Write, () => _resting),
            (track, parameter, pass, after) => editor.CommitPassAsync(track, parameter, pass, after));

        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);
        _transport.Tick(TransportState.Stopped, 0.5);
        await recorder.Committing;

        Assert.Equal(["automation_write_commit"], _hub.Sent.Select(sent => sent.Command));
    }
}
