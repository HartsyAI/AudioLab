using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Core.Realtime;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the page's playback service.</summary>
public sealed class PlaybackServiceTests
{
    private readonly FakeAudioHost _host = new();
    private readonly FakeProjectHub _hub = new();

    private PlaybackService Service() =>
        new(_host, new HttpClient { BaseAddress = new Uri("http://example.test/audiolab/") }, TimeProvider.System);

    private async Task<LiveProject> OpenAsync()
    {
        LiveProject live = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        return live;
    }

    private static HostStatus Status(TransportState state = TransportState.Stopped, long position = 0) =>
        new(state, position, 48000, 1, 0, null, 10, 0, 0.5, 0, default, []);

    private static ProjectChange Change(long revision, ProjectDocument document) =>
        new("song", revision, ProjectJson.Write(document), "track_add");

    /// <summary>The engine starts on the play that asks for it and not before.</summary>
    [Fact]
    public async Task TheEngineStartsOnThePlayThatAsksForItAndNotBefore()
    {
        using PlaybackService playback = Service();
        playback.Seek(2.5);
        playback.SetLoop(true);
        Assert.Empty(_host.Calls);

        await playback.PlayAsync();

        Assert.Equal(PlaybackPhase.Ready, playback.Phase);
        Assert.Equal(["start http://example.test/audiolab/ 100", "seek 2.5", "play"], _host.Calls);
    }

    /// <summary>An engine that cannot start is reported and play does nothing.</summary>
    [Fact]
    public async Task AnEngineThatCannotStartIsReportedAndPlayDoesNothing()
    {
        _host.StartFailure = new AudioHostException("No audio worklet.");
        using PlaybackService playback = Service();

        await playback.PlayAsync();

        Assert.Equal(PlaybackPhase.Failed, playback.Phase);
        Assert.Equal("No audio worklet.", playback.Problem);
        Assert.DoesNotContain("play", _host.Calls);
    }

    /// <summary>The open project goes to the engine on start and on every new revision.</summary>
    [Fact]
    public async Task TheOpenProjectGoesToTheEngineOnStartAndOnEveryNewRevision()
    {
        await using LiveProject live = await OpenAsync();
        using PlaybackService playback = Service();
        playback.Follow(live);
        Assert.Empty(_host.Documents);

        await playback.PlayAsync();
        _hub.Push(Change(5, Song.Empty() with { Name = "Five" }));
        _hub.Push(Change(5, Song.Empty() with { Name = "Again" }));
        _hub.Push(Change(6, Song.Empty() with { Name = "Six" }));

        Assert.Equal([1, 2, 3], _host.Documents.Select(sent => sent.Revision));
        Assert.Contains("Six", _host.Documents[^1].Json);
    }

    /// <summary>Leaving the project lets the engine go because its plan and its clips belonged to the project.</summary>
    [Fact]
    public async Task LeavingTheProjectLetsTheEngineGoAndTheNextPlayStartsItAgain()
    {
        await using LiveProject live = await OpenAsync();
        using PlaybackService playback = Service();
        playback.Follow(live);
        await playback.PlayAsync();
        _host.Report(Status(TransportState.Playing, 1) with { Master = new LevelPair(0.5, 0.5) });

        playback.Unfollow();

        Assert.Equal("dispose", _host.Calls[^1]);
        Assert.Equal(PlaybackPhase.Idle, playback.Phase);
        Assert.Equal(default, playback.Master);
        await playback.PlayAsync();
        Assert.Equal(PlaybackPhase.Ready, playback.Phase);
    }

    /// <summary>The loop covers what is heard and its end follows an edit.</summary>
    [Fact]
    public async Task TheLoopCoversWhatIsHeardAndItsEndFollowsAnEdit()
    {
        ProjectDocument empty = Song.Empty();
        _hub.JoinDocument = ProjectJson.Write(empty.With(Song.NewTrack("t1", 64, Song.Audio("c1", 0, 4))));
        await using LiveProject live = await OpenAsync();
        using PlaybackService playback = Service();
        playback.Follow(live);
        await playback.PlayAsync();

        playback.SetLoop(true);
        Assert.Equal((0, 4), playback.LoopRange);
        _hub.Push(Change(2, empty.With(Song.NewTrack("t1", 64, Song.Audio("c1", 0, 9)))));

        Assert.Equal((0, 9), playback.LoopRange);
        Assert.Equal("loop True 0 9", _host.Calls[^1]);
        playback.SetLoop(false);
        Assert.Null(playback.LoopRange);
        Assert.Equal("loop False 0 0", _host.Calls[^1]);
    }

    /// <summary>The transport position and levels come from what the engine reports.</summary>
    [Fact]
    public async Task TheTransportPositionAndLevelsComeFromWhatTheEngineReports()
    {
        using PlaybackService playback = Service();
        await playback.PlayAsync();

        _host.Report(Status(TransportState.Playing, 96000));
        _host.Report(new WorkletStats(72000, 2048, 2000, 0, 0, true, 0));
        Assert.Equal(TransportState.Playing, playback.Transport);
        Assert.Equal(1.5, playback.Position);

        _host.Report(Status() with { Master = new LevelPair(0.5, 0.25), Tracks = [new TrackLevel("t1", new LevelPair(0.8, 0.8))] });
        Assert.Equal(new LevelPair(0.5, 0.25), playback.Master);
        Assert.Equal(new LevelPair(0.8, 0.8), playback.LevelOf(TrackId.From("t1")));

        _host.Report(Status());
        Assert.True(playback.Master.Left < 0.5 && playback.Master.Left > 0);
    }

    /// <summary>A worker that crashes after it started ends the engine, says why, and the next Play starts it again.</summary>
    [Fact]
    public async Task AWorkerThatCrashesAfterItStartedEndsTheEngineAndTheNextPlayStartsItAgain()
    {
        using PlaybackService playback = Service();
        await playback.PlayAsync();

        _host.Fail("The engine's worker failed: out of memory.");

        Assert.Equal(PlaybackPhase.Failed, playback.Phase);
        Assert.Contains("out of memory", playback.Problem);
        Assert.Contains("Press Play", playback.Problem);
        await playback.PlayAsync();
        Assert.Equal(PlaybackPhase.Ready, playback.Phase);
        Assert.Null(playback.Problem);
    }

    /// <summary>A call the worker refuses shows its reason and leaves the engine and the transport as they were.</summary>
    [Fact]
    public async Task ACallTheWorkerRefusesShowsItsReasonAndPauseAndStopStillReachTheEngine()
    {
        using PlaybackService playback = Service();
        await playback.PlayAsync();

        _host.Reject("Something the worker did not like.");
        playback.Pause();
        playback.Stop();

        Assert.Equal(PlaybackPhase.Ready, playback.Phase);
        Assert.Equal("Something the worker did not like.", playback.Problem);
        Assert.Equal(["pause", "stop"], _host.Calls.TakeLast(2));
    }

    /// <summary>A start that times out leaves the service failed with the reason, not starting for ever.</summary>
    [Fact]
    public async Task AStartThatTimesOutIsAFailureWithTheReasonAndNotAServiceStuckStarting()
    {
        _host.StartFailure = new TimeoutException("The worker did not start.");
        using PlaybackService playback = Service();

        await playback.PlayAsync();

        Assert.Equal(PlaybackPhase.Failed, playback.Phase);
        Assert.Contains("did not start", playback.Problem);
    }

    /// <summary>The toggle goes by what was last asked, so a quick second press pauses and does not send play twice.</summary>
    [Fact]
    public async Task ThePlayToggleGoesByWhatWasLastAskedNotByTheLastReport()
    {
        using PlaybackService playback = Service();

        await playback.TogglePlayAsync();
        await playback.TogglePlayAsync();
        await playback.TogglePlayAsync();

        Assert.Equal(["play", "pause", "play"], _host.Calls.Where(call => call is "play" or "pause"));
    }

    /// <summary>A track the engine stops reporting falls to nothing and a closed project leaves no levels behind.</summary>
    [Fact]
    public async Task ATrackTheEngineStopsReportingFallsToNothing()
    {
        using PlaybackService playback = Service();
        await playback.PlayAsync();
        _host.Report(Status() with { Tracks = [new TrackLevel("t1", new LevelPair(0.8, 0.8))] });

        for (int i = 0; i < 80; i++)
        {
            _host.Report(Status());
        }

        Assert.Equal(default, playback.LevelOf(TrackId.From("t1")));
    }
}
