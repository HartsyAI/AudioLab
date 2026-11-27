using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>A pass that its lane leaves is not written, and one grab at a time is held: a second grab is refused rather than recorded nowhere (D86).</summary>
public sealed class AutomationRecorderGrabTests
{
    private static readonly TrackId _track = TrackId.From("t1");

    /// <summary>A mode change while the pass plays ends it without a commit, and a grab after it records nothing.</summary>
    [Fact]
    public void AModeChangeDuringAPassEndsItWithoutACommit()
    {
        FakePlaybackTransport transport = new();
        AutomationMode mode = AutomationMode.Touch;
        List<string> notices = [];
        List<AutomationPass> commits = [];
        AutomationRecorder recorder = new();
        recorder.Notice += notices.Add;
        recorder.Open(
            transport,
            () => new AutomationTarget(_track, AutomationParameter.Volume, mode, () => 0.4),
            (_, _, pass, _) =>
            {
                commits.Add(pass);
                return Task.CompletedTask;
            });

        transport.Tick(TransportState.Playing, 0);
        transport.Tick(TransportState.Playing, 1.0);
        Assert.True(recorder.Grab(_track, AutomationParameter.Volume, 0.5));
        transport.Tick(TransportState.Playing, 1.2);

        mode = AutomationMode.Off;
        transport.Tick(TransportState.Playing, 1.4);
        Assert.False(recorder.Passing);
        Assert.False(recorder.Grab(_track, AutomationParameter.Volume, 0.6));
        transport.Tick(TransportState.Stopped, 1.5);

        Assert.Empty(commits);
        Assert.Single(notices);
    }

    /// <summary>A second grab while one is held is refused, so the held one keeps its segment and the refusal is visible to the caller.</summary>
    [Fact]
    public void ASecondGrabWhileOneIsHeldIsRefused()
    {
        FakePlaybackTransport transport = new();
        AutomationRecorder recorder = new();
        recorder.Open(
            transport,
            () => new AutomationTarget(_track, AutomationParameter.Volume, AutomationMode.Touch, () => 0.4),
            (_, _, _, _) => Task.CompletedTask);

        transport.Tick(TransportState.Playing, 0);
        transport.Tick(TransportState.Playing, 1.0);

        Assert.True(recorder.Grab(_track, AutomationParameter.Volume, 0.5));
        Assert.True(recorder.Holding);
        Assert.False(recorder.Grab(_track, AutomationParameter.Volume, 0.9));
    }
}
