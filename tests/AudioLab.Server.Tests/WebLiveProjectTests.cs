using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Services;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

/// <summary>The session screen's own services against the real server over the real hub: the whole loop of D20 as the page runs it.</summary>
public sealed class WebLiveProjectTests
{
    private static async Task<string> CreateAsync(RunningServer server)
    {
        return (await server.Services.GetRequiredService<ProjectService>().CreateAsync(UserId.Local, "Song", CancellationToken.None)).Id;
    }

    private static LiveProject NewLive(RunningServer server)
    {
        SignalRProjectHubFactory hubs = new(new Uri(server.Client.BaseAddress!, server.HubPath));
        return new LiveProject(hubs, new CommandSender(CoreCommands.CreateRegistry()));
    }

    [Fact]
    public async Task TheEditsOfTheScreenReachTheDocumentAndEveryOtherOpenClientAndUndoAndRedoAreStepsForAll()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string id = await CreateAsync(server);
        await using LiveProject mine = NewLive(server);
        await using LiveProject theirs = NewLive(server);
        await mine.OpenAsync(id);
        await theirs.OpenAsync(id);
        SessionEditor editor = new(mine);

        EditResult added = await editor.AddTrackAsync();
        TrackId track = TrackId.From(added.Created.Single());
        await editor.SetMutedAsync(track, true);
        await editor.SetSoloedAsync(track, true);
        await editor.RenameTrackAsync(track, "Lead");

        Track here = mine.Document!.Tracks.Single();
        Assert.Equal(("Lead", true, true), (here.Name, here.Muted, here.Soloed));
        await Until(() => theirs.Revision == mine.Revision);
        Assert.Equal(mine.Document, theirs.Document);

        await editor.UndoAsync();
        Assert.Equal("Track 1", mine.Document.Tracks.Single().Name);
        await Until(() => theirs.Revision == mine.Revision);
        Assert.Equal("Track 1", theirs.Document!.Tracks.Single().Name);

        await editor.RedoAsync();
        Assert.Equal("Lead", mine.Document.Tracks.Single().Name);
    }

    [Fact]
    public async Task AClipMoveIsOneUndoStepWithAbsoluteTargetsAndTheServersReasonsReachTheUser()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string id = await CreateAsync(server);
        await using LiveProject live = NewLive(server);
        await live.OpenAsync(id);
        SessionEditor editor = new(live);
        TrackId first = TrackId.From((await editor.AddTrackAsync()).Created.Single());
        TrackId second = TrackId.From((await editor.AddTrackAsync()).Created.Single());
        ProjectId project = live.Document!.Id;
        AddClip add = new() { Project = project, Track = first, Media = MediaId.From("media-1"), Duration = 4, Start = 8 };
        CommandReply clip = await live.SendAsync(add);
        ClipId clipId = ClipId.From(clip.Created.Single());

        EditResult moved = await editor.MoveClipAsync(clipId, 12.125, second);

        Assert.True(moved.Succeeded);
        Track target = live.Document.Tracks.Single(track => track.Id == second);
        Assert.Equal(12.125, target.Clips.Single().Start);
        Assert.Empty(live.Document.Tracks.Single(track => track.Id == first).Clips);

        await editor.UndoAsync();
        Assert.Equal(8, live.Document.Tracks.Single(track => track.Id == first).Clips.Single().Start);
        Assert.Empty(live.Document.Tracks.Single(track => track.Id == second).Clips);

        EditResult refused = await editor.MoveClipAsync(clipId, -5, null);
        Assert.False(refused.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(refused.Refusal));
    }

    [Fact]
    public async Task ADeletedProjectReachesTheOpenScreenAsAProblem()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        string id = await CreateAsync(server);
        await using LiveProject live = NewLive(server);
        await live.OpenAsync(id);

        await server.Services.GetRequiredService<ProjectService>().DeleteAsync(UserId.Local, ProjectId.From(id), CancellationToken.None);
        await Until(() => live.Problem is not null);

        Assert.Equal("This project was deleted.", live.Problem);
    }

    [Fact]
    public async Task OpeningAProjectThatIsNotTheUsersIsRefusedWithTheServersWords()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        await using LiveProject live = NewLive(server);

        HubException refused = await Assert.ThrowsAsync<HubException>(() => live.OpenAsync("prj_nothing"));

        Assert.False(string.IsNullOrWhiteSpace(refused.Message));
    }

    private static async Task Until(Func<bool> condition)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < end)
        {
            await Task.Delay(20);
        }
        Assert.True(condition(), "The condition did not become true in 10 s.");
    }
}
