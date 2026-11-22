using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Tests;

public sealed class SessionEditorTests
{
    private readonly FakeProjectHub _hub = new();

    private async Task<(LiveProject Live, SessionEditor Editor)> OpenAsync()
    {
        LiveProject live = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        _hub.Calls.Clear();
        return (live, new SessionEditor(live));
    }

    private string[] Applied() => [.. _hub.Sent.Select(sent => sent.Command)];

    [Fact]
    public async Task EachEditIsOneCommandUnderItsWireName()
    {
        (LiveProject live, SessionEditor editor) = await OpenAsync();
        await using (live)
        {
            _hub.Reply = new CommandReply(true, false, 1, [], []);

            await editor.AddTrackAsync();
            await editor.RenameTrackAsync(TrackId.From("t1"), "Lead");
            await editor.SetMutedAsync(TrackId.From("t1"), true);
            await editor.SetSoloedAsync(TrackId.From("t1"), true);

            Assert.Equal(["track_add", "track_rename", "mixer_set_track_muted", "mixer_set_track_soloed"], Applied());
        }
    }

    [Fact]
    public async Task AMoveIsOneGestureOfOneCommandWithAbsoluteTargetsAndTheGestureIsClosedAfterIt()
    {
        (LiveProject live, SessionEditor editor) = await OpenAsync();
        await using (live)
        {
            _hub.Reply = new CommandReply(true, false, 1, [], []);

            await editor.MoveClipAsync(ClipId.From("c1"), 12.5, TrackId.From("t2"));

            (string command, string json, string? gesture) = Assert.Single(_hub.Sent);
            Assert.Equal("clip_move", command);
            Assert.NotNull(gesture);
            Assert.Equal($"end song {gesture}", _hub.Calls[^1]);
            MoveClip sent = Assert.IsType<MoveClip>(CoreCommands.CreateRegistry().Parse(command, json).Command);
            Assert.Equal(12.5, sent.Start);
            Assert.Equal(TrackId.From("t2"), sent.Track);
        }
    }

    [Fact]
    public async Task AMoveWithoutATrackChangeSendsNoTrack()
    {
        (LiveProject live, SessionEditor editor) = await OpenAsync();
        await using (live)
        {
            _hub.Reply = new CommandReply(true, false, 1, [], []);

            await editor.MoveClipAsync(ClipId.From("c1"), 4, null);

            Assert.DoesNotContain("\"track\"", _hub.Sent[0].Json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheServersReasonsComeBackAsWordsAndNothingIsThrown()
    {
        (LiveProject live, SessionEditor editor) = await OpenAsync();
        await using (live)
        {
            _hub.Reply = FakeProjectHub.Refused(CommandErrorCode.Rejected, "a track name may not be blank");

            EditResult result = await editor.RenameTrackAsync(TrackId.From("t1"), " ");

            Assert.False(result.Succeeded);
            Assert.Equal("A track name may not be blank.", result.Refusal);
        }
    }

    [Fact]
    public async Task AnEditThatCreatesSomethingReturnsItsIds()
    {
        (LiveProject live, SessionEditor editor) = await OpenAsync();
        await using (live)
        {
            _hub.Reply = new CommandReply(true, false, 1, [], ["trk_new"]);

            EditResult result = await editor.AddTrackAsync();

            Assert.Equal(["trk_new"], result.Created);
        }
    }

    [Fact]
    public async Task AnEditBeforeTheProjectIsOpenOrOnALostConnectionIsRefusedNotThrown()
    {
        LiveProject closed = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        EditResult notOpen = await new SessionEditor(closed).AddTrackAsync();
        Assert.False(notOpen.Succeeded);

        (LiveProject live, SessionEditor editor) = await OpenAsync();
        await using (live)
        {
            _hub.ThrowOnApply = new InvalidOperationException("The 'InvokeCoreAsync' method cannot be called if the connection is not active");
            EditResult lost = await editor.SetMutedAsync(TrackId.From("t1"), true);
            Assert.False(lost.Succeeded);
            Assert.Contains("connection", lost.Refusal, StringComparison.Ordinal);

            _hub.ThrowOnApply = new HubException("There is no such project.");
            EditResult gone = await editor.AddTrackAsync();
            Assert.Equal("There is no such project.", gone.Refusal);
        }
    }

    [Fact]
    public async Task UndoAndRedoAreStepsForEveryoneAndANothingToUndoReplyIsASuccessThatChangedNothing()
    {
        (LiveProject live, SessionEditor editor) = await OpenAsync();
        await using (live)
        {
            _hub.Reply = new CommandReply(true, false, 1, [], []);

            EditResult undo = await editor.UndoAsync();
            await editor.RedoAsync();

            Assert.True(undo.Succeeded);
            Assert.False(undo.Changed);
            Assert.Equal(["undo song", "redo song"], _hub.Calls);
        }
    }
}
