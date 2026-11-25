using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The recorder on a fake transport and a fake hub: what a pass records, what it commits, and what a drop leaves (D72, D86).</summary>
public sealed class AutomationRecorderTests
{
    private readonly FakePlaybackTransport _transport = new();

    private readonly List<(AutomationPass Pass, AutomationParameter Parameter, AutomationMode? Mode)> _commits = [];

    private readonly List<string> _notices = [];

    private double _resting = 0.4;

    private static readonly TrackId _track = TrackId.From("t1");

    private AutomationRecorder Open(AutomationMode mode, AutomationParameter parameter = AutomationParameter.Volume)
    {
        AutomationRecorder recorder = new();
        recorder.Notice += _notices.Add;
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

    private async Task<LiveProject> OpenHubAsync(FakeProjectHub hub)
    {
        LiveProject live = new(new FakeHubFactory(hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        hub.Calls.Clear();
        hub.Reply = new CommandReply(true, true, 2, [], []);
        return live;
    }

    /// <summary>A write pass recorded across ticks is one segment, and its commit and mode change are one gesture on the hub.</summary>
    [Fact]
    public async Task AWritePassRecordedAcrossTicksCommitsOneGesture()
    {
        FakeProjectHub hub = new() { JoinDocument = ProjectJson.Write(Song.Empty().With(Song.NewTrack("t1"))) };
        await using LiveProject live = await OpenHubAsync(hub);
        AutomationEditor editor = new(live);
        List<Task> commits = [];
        AutomationRecorder recorder = new();
        recorder.Open(
            _transport,
            () => new AutomationTarget(_track, AutomationParameter.Volume, AutomationMode.Write, () => _resting),
            (track, parameter, pass, after) =>
            {
                Task commit = editor.CommitPassAsync(track, parameter, pass, after);
                commits.Add(commit);
                return commit;
            });

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
        await Task.WhenAll(commits);

        Assert.Equal(["automation_write_commit", "automation_set_mode"], hub.Sent.Select(sent => sent.Command));
        string? gesture = hub.Sent[0].Gesture;
        Assert.NotNull(gesture);
        Assert.All(hub.Sent, sent => Assert.Equal(gesture, sent.Gesture));
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

    /// <summary>A write pass that the playhead seeks across starts a new segment at the seek, so no ramp is drawn over the skipped part.</summary>
    [Fact]
    public void AWriteSeekStartsANewSegmentAtTheSeek()
    {
        AutomationRecorder recorder = Open(AutomationMode.Write);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);
        _transport.Tick(TransportState.Playing, 1.0);

        _transport.Tick(TransportState.Playing, 5.0);
        _transport.Tick(TransportState.Playing, 5.5);
        _transport.Tick(TransportState.Stopped, 6.0);

        Assert.False(recorder.Passing);
        (AutomationPass pass, _, _) = Assert.Single(_commits);
        Assert.Equal(2, pass.Segments.Count);
        Assert.Equal(1.0, pass.Segments[0][^1].Time, 6);
        Assert.Equal(5.0, pass.Segments[1][0].Time, 6);
        Assert.DoesNotContain(pass.Segments.SelectMany(segment => segment), point => point.Time > 1.0 && point.Time < 5.0);
    }

    /// <summary>A touch pass records only the grab-to-release span, so the old curve outside it is kept.</summary>
    [Fact]
    public void ATouchPassKeepsTheCurveOutsideTheTouchedSpan()
    {
        AutomationRecorder recorder = Open(AutomationMode.Touch);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 1.0);

        recorder.Grab(_track, AutomationParameter.Volume, 0.9);
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

        recorder.Grab(_track, AutomationParameter.Volume, 0.6);
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

    /// <summary>Only the lane the control is on records: a grab on another parameter of the same lane starts nothing.</summary>
    [Fact]
    public void AGrabOfAnotherParameterDoesNotRecord()
    {
        AutomationRecorder recorder = Open(AutomationMode.Touch);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);

        Assert.True(recorder.RecordsLane(_track, AutomationParameter.Volume));
        Assert.False(recorder.RecordsLane(_track, AutomationParameter.Pan));
        Assert.False(recorder.RecordsLane(TrackId.From("t2"), AutomationParameter.Volume));
        Assert.True(recorder.Records(_track));

        recorder.Grab(_track, AutomationParameter.Pan, 0.3);
        recorder.Release(_track, 0.3);
        _transport.Tick(TransportState.Playing, 1.0);
        _transport.Tick(TransportState.Stopped, 1.0);

        Assert.Empty(_commits);
    }

    /// <summary>An engine that stops or fails ends the pass without a commit, and the page is told why.</summary>
    [Fact]
    public void AnEngineStopDropsThePassAndSaysSo()
    {
        AutomationRecorder recorder = Open(AutomationMode.Write);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);

        _transport.EngineRunning = false;
        _transport.Tick(TransportState.Stopped, 0.5);

        Assert.False(recorder.Passing);
        Assert.Empty(_commits);
        Assert.Contains(_notices, text => text.Contains("audio engine stopped", StringComparison.Ordinal));
    }

    /// <summary>A lost connection drops the pass without a commit, and says why; with no pass there is nothing to say.</summary>
    [Fact]
    public void ALostConnectionDropsThePassAndSaysSo()
    {
        AutomationRecorder recorder = Open(AutomationMode.Write);
        recorder.Lost("The connection was lost, so the pass was not written.");
        Assert.Empty(_notices);

        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);
        recorder.Lost("The connection was lost, so the pass was not written.");
        _transport.Tick(TransportState.Stopped, 1.0);

        Assert.False(recorder.Passing);
        Assert.Empty(_commits);
        Assert.Equal(["The connection was lost, so the pass was not written."], _notices);
    }

    /// <summary>Closing the recorder, which a project switch does, drops the pass and stops the transport from reaching it.</summary>
    [Fact]
    public void AProjectSwitchDropsThePassWithoutCommittingOrSaying()
    {
        AutomationRecorder recorder = Open(AutomationMode.Write);
        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);

        recorder.Close();
        _transport.Tick(TransportState.Stopped, 1.0);

        Assert.False(recorder.Passing);
        Assert.Empty(_commits);
        Assert.Empty(_notices);
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

    /// <summary>A new pass waits until the commit of the last one has landed, so a pause and a quick resume cannot write two passes at once.</summary>
    [Fact]
    public void APassWaitsUntilTheLastCommitHasLanded()
    {
        TaskCompletionSource landed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AutomationRecorder recorder = new();
        recorder.Notice += _notices.Add;
        recorder.Open(
            _transport,
            () => new AutomationTarget(_track, AutomationParameter.Volume, AutomationMode.Write, () => _resting),
            (_, _, _, _) => landed.Task);

        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);
        _transport.Tick(TransportState.Stopped, 0.5);
        _transport.Tick(TransportState.Playing, 0.5);

        Assert.False(recorder.Passing);
        Assert.Contains(_notices, text => text.Contains("still being written", StringComparison.Ordinal));

        _transport.Tick(TransportState.Stopped, 0.5);
        landed.SetResult();
        _transport.Tick(TransportState.Playing, 0);

        Assert.True(recorder.Passing);
    }

    /// <summary>A refused first part changes nothing: no mode change is sent after it.</summary>
    [Fact]
    public async Task ARefusedFirstPartSendsNoModeChange()
    {
        FakeProjectHub hub = new() { JoinDocument = ProjectJson.Write(Song.Empty().With(Song.NewTrack("t1"))) };
        await using LiveProject live = await OpenHubAsync(hub);
        hub.ReplyForApply = count => count == 1 ? FakeProjectHub.Refused(CommandErrorCode.Rejected, "The lane is full.") : null;
        AutomationEditor editor = new(live);
        List<Task> commits = [];
        AutomationRecorder recorder = new();
        recorder.Open(
            _transport,
            () => new AutomationTarget(_track, AutomationParameter.Volume, AutomationMode.Write, () => _resting),
            (track, parameter, pass, after) =>
            {
                Task commit = editor.CommitPassAsync(track, parameter, pass, after);
                commits.Add(commit);
                return commit;
            });

        _transport.Tick(TransportState.Playing, 0);
        _transport.Tick(TransportState.Playing, 0.5);
        _transport.Tick(TransportState.Stopped, 0.5);
        await Task.WhenAll(commits);

        Assert.Equal(["automation_write_commit"], hub.Sent.Select(sent => sent.Command));
    }

    /// <summary>A refusal after a part landed keeps that part, sends no mode change, and says how many parts were written.</summary>
    [Fact]
    public async Task ARefusedLaterPartSaysHowManyLanded()
    {
        FakeProjectHub hub = new() { JoinDocument = ProjectJson.Write(Song.Empty().With(Song.NewTrack("t1"))) };
        await using LiveProject live = await OpenHubAsync(hub);
        hub.ReplyForApply = count => count == 2 ? FakeProjectHub.Refused(CommandErrorCode.Rejected, "The lane is full.") : null;
        AutomationEditor editor = new(live);
        AutomationPass pass = new(
            [
                [new WrittenPoint(1, 0.5)],
                [new WrittenPoint(3, 0.6)],
            ],
            ReturnToOld: true,
            Truncated: false);

        EditResult result = await editor.CommitPassAsync(_track, AutomationParameter.Volume, pass, null);

        Assert.Equal(["automation_write_commit", "automation_write_commit"], hub.Sent.Select(sent => sent.Command));
        Assert.NotNull(result.Refusal);
        Assert.Contains("1 of 2 recorded parts were written", result.Refusal, StringComparison.Ordinal);
    }
}
