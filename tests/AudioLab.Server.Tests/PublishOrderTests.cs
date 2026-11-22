using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

public sealed class PublishOrderTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    [Fact]
    public async Task ADeleteWaitsForAChangeThatIsBeingAnnouncedSoTheDeletionNeverOvertakesIt()
    {
        using TempFolder folder = new();
        OrderingEvents events = new();
        await using TestStore store = TestStore.On(folder.Path, registry: TestCommandRegistry.Create(), events: events);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        await store.Service.OpenAsync(Users.Alice, id, _none);
        events.HoldChanges();
        Task apply = store.Edits.ApplyAsync(Users.Alice, id, AudioLab.Core.Commands.ClientId.From("c"), "set_bpm", "{\"project\":\"" + id.Value + "\",\"bpm\":90}", null, _none);
        await events.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Task delete = store.Service.DeleteAsync(Users.Alice, id, _none);
        await Task.Delay(150);
        Assert.False(delete.IsCompleted);
        events.Release();
        await apply;
        await delete;
        Assert.Equal(["changed:2", "deleted"], events.Events);
    }

    [Fact]
    public async Task AJoinThatWaitsForAnInFlightDeletionOfAProjectThatWasNotOpenFindsNoProject()
    {
        using TempFolder folder = new();
        OrderingEvents events = new();
        await using TestStore store = TestStore.On(folder.Path, registry: TestCommandRegistry.Create(), events: events);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        Assert.Null(store.Open.SessionIfOpen(Users.Alice, id));
        events.HoldDeletion();
        Task delete = store.Service.DeleteAsync(Users.Alice, id, _none);
        await events.DeleteEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        bool subscribed = false;
        Task<ProjectSnapshot> join = store.Edits.JoinAsync(Users.Alice, id, _ =>
        {
            subscribed = true;
            return Task.CompletedTask;
        }, _ => Task.CompletedTask, _none);
        await Task.Delay(150);
        Assert.False(join.IsCompleted);

        events.ReleaseDeletion();
        await delete;
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => join);
        Assert.False(subscribed);
        Assert.Equal(["deleted"], events.Events);
    }

    [Fact]
    public async Task AJoinAheadOfADeletionIsAnnouncedToAndNeverLeftHoldingTheDeletedProject()
    {
        using TempFolder folder = new();
        OrderingEvents events = new();
        await using TestStore store = TestStore.On(folder.Path, registry: TestCommandRegistry.Create(), events: events);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        TaskCompletionSource subscribing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource carryOn = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> log = [];
        Task<ProjectSnapshot> join = store.Edits.JoinAsync(Users.Alice, id, async _ =>
        {
            log.Add("subscribed");
            subscribing.SetResult();
            await carryOn.Task;
        }, _ => Task.CompletedTask, _none);
        await subscribing.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Task delete = store.Service.DeleteAsync(Users.Alice, id, _none);
        await Task.Delay(150);
        Assert.False(delete.IsCompleted);
        carryOn.SetResult();
        await join;
        await delete;
        Assert.Equal(["subscribed"], log);
        Assert.Equal(["deleted"], events.Events);
    }

    [Fact]
    public async Task AJoinWhoseProjectIsClosedWhileItSubscribesIsTakenBackOutAndToldTheProjectIsClosing()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: TestCommandRegistry.Create());
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        List<string> log = [];
        ProjectNotFoundException error = await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Edits.JoinAsync(Users.Alice, id, async _ =>
        {
            log.Add("subscribed");
            await store.Open.CloseAsync(Users.Alice, id, _none);
        }, _ =>
        {
            log.Add("unsubscribed");
            return Task.CompletedTask;
        }, _none));
        Assert.Equal(["subscribed", "unsubscribed"], log);
        Assert.Contains("closing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOrderBelongsToTheProjectAndTwoUsersOrTwoProjectsNeverWaitForEachOther()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId one = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "One", _none)).Id);
        ProjectId two = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Two", _none)).Id);
        using IDisposable held = await store.Open.EnterOrderAsync(Users.Alice, one, _none);
        using IDisposable other = await store.Open.EnterOrderAsync(Users.Alice, two, _none).WaitAsync(TimeSpan.FromSeconds(5));
        using IDisposable another = await store.Open.EnterOrderAsync(Users.Bob, one, _none).WaitAsync(TimeSpan.FromSeconds(5));
        Task<IDisposable> second = store.Open.EnterOrderAsync(Users.Alice, one, _none);
        await Task.Delay(100);
        Assert.False(second.IsCompleted);
        held.Dispose();
        held.Dispose();
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    [Fact]
    public async Task ACommandThatWouldMakeTheProjectTooLargeToStoreIsRefusedAndNothingIsAnnounced()
    {
        using TempFolder folder = new();
        OrderingEvents events = new();
        await using TestStore store = TestStore.On(folder.Path, new StoreOptions { MaxFileBytes = 4096 }, TestCommandRegistry.Create(), events);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        string name = new('m', 190);
        AudioLab.Core.Commands.ClientId client = AudioLab.Core.Commands.ClientId.From("c");
        int accepted = 0;
        string[] created = [];
        while (true)
        {
            string json = "{\"project\":\"" + id.Value + "\",\"time\":" + accepted + ",\"name\":\"" + name + "\"}";
            Realtime.CommandReply reply = await store.Edits.ApplyAsync(Users.Alice, id, client, "add_marker", json, null, _none);
            if (!reply.Succeeded)
            {
                Assert.Equal(AudioLab.Core.Commands.CommandErrorCode.Limit, reply.Errors.Single().Code);
                break;
            }
            created = reply.Created;
            accepted++;
            Assert.True(accepted < 100, "the limit was never reached");
        }
        ProjectSnapshot snapshot = store.Open.SnapshotIfOpen(Users.Alice, id)!;
        Assert.Equal(accepted + 1, snapshot.Revision);
        Assert.Equal(accepted, snapshot.Document.Markers.Count);
        Assert.Equal(created[0], snapshot.Document.Markers[^1].Id.Value);
        Assert.Equal(accepted, events.Events.Count);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(ProjectJson.Write(snapshot.Document)) <= 4096);
    }
}
