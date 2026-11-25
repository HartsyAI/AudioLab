using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the commit of a recorded pass against a fake hub: what is sent, and what a refusal leaves behind (D25).</summary>
public sealed class AutomationEditorTests
{
    private readonly FakeProjectHub _hub = new()
    {
        JoinDocument = ProjectJson.Write(Song.Empty().With(Song.NewTrack("t1"))),
    };

    private async Task<LiveProject> OpenAsync()
    {
        LiveProject live = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        _hub.Calls.Clear();
        _hub.Reply = new CommandReply(true, true, 2, [], []);
        return live;
    }

    private static AutomationPass TwoSegments() => new(
        [[new WrittenPoint(1, 0.5), new WrittenPoint(2, 0.6)], [new WrittenPoint(3, 0.7)]],
        ReturnToOld: true,
        Truncated: false);

    /// <summary>A pass that cannot be written is refused before anything is sent, so it changes nothing.</summary>
    [Fact]
    public async Task APassOutsideTheLaneIsRefusedBeforeAnythingIsSent()
    {
        await using LiveProject live = await OpenAsync();
        AutomationEditor editor = new(live);
        AutomationPass pass = new([[new WrittenPoint(1, 9)]], ReturnToOld: false, Truncated: false);

        EditResult result = await editor.CommitPassAsync(TrackId.From("t1"), AutomationParameter.Pan, pass, null);

        Assert.False(result.Succeeded);
        Assert.Contains("range", result.Refusal);
        Assert.Empty(_hub.Sent);
    }

    /// <summary>A pass whose points run backwards is refused before anything is sent.</summary>
    [Fact]
    public async Task APassOutOfTimeOrderIsRefusedBeforeAnythingIsSent()
    {
        await using LiveProject live = await OpenAsync();
        AutomationEditor editor = new(live);
        AutomationPass pass = new([[new WrittenPoint(2, 0.5)], [new WrittenPoint(1, 0.5)]], ReturnToOld: true, Truncated: false);

        EditResult result = await editor.CommitPassAsync(TrackId.From("t1"), AutomationParameter.Volume, pass, null);

        Assert.False(result.Succeeded);
        Assert.Empty(_hub.Sent);
    }

    /// <summary>When every segment lands, each is a commit, and the mode change follows them in the same gesture.</summary>
    [Fact]
    public async Task EverySegmentLandsThenTheModeChangesInOneGesture()
    {
        await using LiveProject live = await OpenAsync();
        AutomationEditor editor = new(live);

        EditResult result = await editor.CommitPassAsync(TrackId.From("t1"), AutomationParameter.Volume, TwoSegments(), AutomationMode.Touch);

        Assert.True(result.Succeeded);
        Assert.Equal(["automation_write_commit", "automation_write_commit", "automation_set_mode"], _hub.Sent.Select(sent => sent.Command));
        string? gesture = _hub.Sent[0].Gesture;
        Assert.NotNull(gesture);
        Assert.All(_hub.Sent, sent => Assert.Equal(gesture, sent.Gesture));
    }

    /// <summary>A refusal of a later segment says how many landed, and no mode change is sent after it.</summary>
    [Fact]
    public async Task ARefusedSegmentSaysWhatLandedAndSendsNoModeChange()
    {
        await using LiveProject live = await OpenAsync();
        _hub.ReplyForApply = count => count == 2
            ? FakeProjectHub.Refused(CommandErrorCode.Rejected, "The lane is full.")
            : null;
        AutomationEditor editor = new(live);

        EditResult result = await editor.CommitPassAsync(TrackId.From("t1"), AutomationParameter.Volume, TwoSegments(), AutomationMode.Touch);

        Assert.False(result.Succeeded);
        Assert.Contains("1 of 2", result.Refusal);
        Assert.Equal(2, _hub.Sent.Count);
    }

    /// <summary>A refused mode change after the commits says the lane was written, so a partial edit is not reported as none.</summary>
    [Fact]
    public async Task ARefusedModeChangeSaysTheLaneWasWritten()
    {
        await using LiveProject live = await OpenAsync();
        _hub.ReplyForApply = count => count == 3
            ? FakeProjectHub.Refused(CommandErrorCode.Rejected, "No such track.")
            : null;
        AutomationEditor editor = new(live);

        EditResult result = await editor.CommitPassAsync(TrackId.From("t1"), AutomationParameter.Volume, TwoSegments(), AutomationMode.Touch);

        Assert.False(result.Succeeded);
        Assert.Contains("was written", result.Refusal);
        Assert.Equal(3, _hub.Sent.Count);
    }
}
