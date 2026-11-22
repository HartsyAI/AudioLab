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
    public async Task AJoinThatWaitedForItsTurnWhileTheProjectWasDeletedFindsNoProjectAndSubscribesNothing()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: TestCommandRegistry.Create());
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        ProjectSession session = await store.Open.OpenAsync(Users.Alice, id, _none);
        await session.PublishOrder.WaitAsync(_none);
        bool subscribed = false;
        Task<ProjectSnapshot> join = store.Edits.JoinAsync(Users.Alice, id, _ =>
        {
            subscribed = true;
            return Task.CompletedTask;
        }, _none);
        await Task.Delay(100);
        Assert.False(join.IsCompleted);

        Assert.True(await store.Open.DeleteAsync(Users.Alice, id, _none));
        session.PublishOrder.Release();
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => join);
        Assert.False(subscribed);
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
