using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

public sealed class SessionTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    [Fact]
    public async Task ASessionIsTheOneDocumentEveryCallerOfTheProjectShares()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = await CreateAsync(store, Users.Alice);
        ProjectSession[] sessions = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.Open.OpenAsync(Users.Alice, id, _none)));
        Assert.All(sessions, session => Assert.Same(sessions[0], session));
        sessions[0].Update(document => document with { Bpm = 90 });
        Assert.Equal(90, (await store.Service.OpenAsync(Users.Alice, id, _none)).Document.Bpm);
    }

    [Fact]
    public async Task AChangeRaisesTheRevisionAndAnEqualDocumentDoesNot()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        Assert.Equal(1, session.Snapshot.Revision);
        ProjectSnapshot same = session.Update(document => document with { Bpm = document.Bpm });
        Assert.Equal(1, same.Revision);
        Assert.False(session.IsUnsaved);
        ProjectSnapshot changed = session.Update(document => document with { Bpm = 101 });
        Assert.Equal(2, changed.Revision);
        Assert.True(session.IsUnsaved);
        Assert.Equal(101, session.Snapshot.Document.Bpm);
    }

    [Fact]
    public async Task AChangeThatBreaksTheRulesIsRefusedAndLeavesTheDocumentAsItWas()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        ProjectSnapshot before = session.Snapshot;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => session.Update(document => document with { Bpm = 0 }));
        Assert.Contains(error.Problems, problem => problem.StartsWith("bpm", StringComparison.Ordinal));
        Assert.Same(before, session.Snapshot);
        Assert.False(session.IsUnsaved);
    }

    [Fact]
    public async Task AChangeCannotMoveAProjectToAnotherIdOrOwner()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        Assert.Throws<InvalidOperationException>(() => session.Update(document => document with { Owner = Users.Bob }));
        Assert.Throws<InvalidOperationException>(() => session.Update(document => document with { Id = ProjectId.From("prj_other") }));
        Assert.Equal(1, session.Snapshot.Revision);
    }

    [Fact]
    public async Task ConcurrentChangesAreAppliedOneAtATimeAndNoneIsLost()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        const int Count = 64;
        await Task.WhenAll(Enumerable.Range(1, Count).Select(i => Task.Run(() => session.Update(document =>
        {
            Marker marker = new() { Id = MarkerId.From($"mrk_{i}"), Time = i, Name = $"m{i}" };
            return document with { Markers = [.. document.Markers.Append(marker).OrderBy(m => m.Time)] };
        }))));
        Assert.Equal(Count, session.Snapshot.Document.Markers.Count);
        Assert.Equal(Count + 1, session.Snapshot.Revision);
    }

    [Fact]
    public async Task ClosingWritesWhatIsUnsavedAndTheNextOpenReadsIt()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        session.Update(document => document with { Name = "Changed" });
        await store.Open.CloseAsync(Users.Alice, session.Id, _none);
        Assert.False(session.IsUnsaved);
        Assert.Equal("Changed", (await store.Projects.ReadAsync(Users.Alice, session.Id, _none)).Name);
        ProjectSession reopened = await store.Open.OpenAsync(Users.Alice, session.Id, _none);
        Assert.NotSame(session, reopened);
        Assert.Equal("Changed", reopened.Snapshot.Document.Name);
    }

    [Fact]
    public async Task AStaleSessionCannotApplyAChangeOrSave()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession closed = await OpenNewAsync(store);
        await store.Open.CloseAsync(Users.Alice, closed.Id, _none);
        Assert.Throws<SessionClosedException>(() => closed.Update(document => document with { Bpm = 99 }));
        await Assert.ThrowsAsync<SessionClosedException>(() => closed.SaveAsync(_none));

        ProjectSession deleted = await OpenNewAsync(store);
        await store.Service.DeleteAsync(Users.Alice, deleted.Id, _none);
        Assert.Throws<SessionClosedException>(() => deleted.Update(document => document with { Bpm = 99 }));
        await Assert.ThrowsAsync<SessionClosedException>(() => deleted.SaveAsync(_none));
        Assert.False(File.Exists(store.Paths.ProjectFile(Users.Alice, deleted.Id)));
    }

    [Fact]
    public async Task ASessionDoesNotLeaveTheRegistryUntilItsUnsavedChangesAreOnDisk()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        session.Update(document => document with { Name = "Unsaved" });
        store.Observer = (stage, temp) =>
        {
            if (stage == AtomicWriteStage.TempCreated)
            {
                throw new IOException("disk full");
            }
        };
        await Assert.ThrowsAsync<IOException>(() => store.Open.CloseAsync(Users.Alice, session.Id, _none));
        Assert.True(session.IsUnsaved);
        Assert.Same(session, await store.Open.OpenAsync(Users.Alice, session.Id, _none));
        Assert.Equal("Unsaved", session.Update(document => document).Document.Name);

        store.Observer = null;
        await store.Open.CloseAsync(Users.Alice, session.Id, _none);
        Assert.Equal("Unsaved", (await store.Projects.ReadAsync(Users.Alice, session.Id, _none)).Name);
    }

    [Fact]
    public async Task AutosaveWaitsForTheQuietPeriodAndEachChangeRestartsIt()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        session.Update(document => document with { Bpm = 91 });
        store.Time.Advance(TimeSpan.FromSeconds(1.9));
        await session.SettleAsync();
        Assert.True(session.IsUnsaved);
        session.Update(document => document with { Bpm = 92 });
        store.Time.Advance(TimeSpan.FromSeconds(1.9));
        await session.SettleAsync();
        Assert.True(session.IsUnsaved);
        Assert.Equal(120, (await store.Projects.ReadAsync(Users.Alice, session.Id, _none)).Bpm);

        store.Time.Advance(TimeSpan.FromSeconds(0.2));
        await session.SettleAsync();
        Assert.False(session.IsUnsaved);
        Assert.Equal(3, session.SavedRevision);
        Assert.Equal(92, (await store.Projects.ReadAsync(Users.Alice, session.Id, _none)).Bpm);
    }

    [Fact]
    public async Task AProjectThatKeepsChangingIsStillWrittenWithinTheMaximumDelay()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        double bpm = 100;
        for (int second = 0; second < 14; second++)
        {
            bpm++;
            session.Update(document => document with { Bpm = bpm });
            store.Time.Advance(TimeSpan.FromSeconds(1));
            await session.SettleAsync();
            Assert.True(session.IsUnsaved, $"written too early at second {second}");
        }
        bpm++;
        session.Update(document => document with { Bpm = bpm });
        store.Time.Advance(TimeSpan.FromSeconds(1));
        await session.SettleAsync();
        Assert.False(session.IsUnsaved);
        Assert.Equal(bpm, (await store.Projects.ReadAsync(Users.Alice, session.Id, _none)).Bpm);
    }

    [Fact]
    public async Task AFailedAutosaveStaysUnsavedAndIsRetried()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        store.Observer = (stage, temp) => throw new IOException("disk full");
        session.Update(document => document with { Bpm = 77 });
        store.Time.Advance(TimeSpan.FromSeconds(2));
        await session.SettleAsync();
        Assert.True(session.IsUnsaved);
        Assert.Equal(120, (await store.Projects.ReadAsync(Users.Alice, session.Id, _none)).Bpm);

        store.Observer = null;
        store.Time.Advance(store.Options.AutosaveMaxDelay);
        await session.SettleAsync();
        Assert.False(session.IsUnsaved);
        Assert.Equal(77, (await store.Projects.ReadAsync(Users.Alice, session.Id, _none)).Bpm);
    }

    [Fact]
    public async Task StoppingTheServerWritesEveryUnsavedProject()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession a = await OpenNewAsync(store);
        ProjectSession b = await OpenNewAsync(store, Users.Bob);
        a.Update(document => document with { Name = "A, edited" });
        b.Update(document => document with { Name = "B, edited" });
        Assert.Equal(0, await store.Open.FlushAllAsync(_none));
        await store.Open.StopAsync(_none);
        Assert.Equal("A, edited", (await store.Projects.ReadAsync(Users.Alice, a.Id, _none)).Name);
        Assert.Equal("B, edited", (await store.Projects.ReadAsync(Users.Bob, b.Id, _none)).Name);
        Assert.False(a.IsUnsaved || b.IsUnsaved);
    }

    [Fact]
    public async Task ADeleteWaitsForAWriteInFlightAndNoAutosaveBringsTheFileBack()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession session = await OpenNewAsync(store);
        using ManualResetEventSlim writing = new();
        using ManualResetEventSlim release = new();
        store.Observer = (stage, temp) =>
        {
            if (stage == AtomicWriteStage.Flushed)
            {
                writing.Set();
                release.Wait(TimeSpan.FromSeconds(10));
            }
        };
        session.Update(document => document with { Name = "Edited" });
        Task advance = Task.Run(() => store.Time.Advance(TimeSpan.FromSeconds(2)));
        Assert.True(writing.Wait(TimeSpan.FromSeconds(10)));

        Task delete = store.Service.DeleteAsync(Users.Alice, session.Id, _none);
        await Task.Delay(100);
        Assert.False(delete.IsCompleted);
        release.Set();
        await delete;
        await advance;
        await session.SettleAsync();
        store.Time.Advance(TimeSpan.FromMinutes(1));
        await session.SettleAsync();

        Assert.False(File.Exists(store.Paths.ProjectFile(Users.Alice, session.Id)));
        Assert.Empty((await store.Service.ListAsync(Users.Alice, _none)).Projects);
    }

    [Fact]
    public async Task TheSameProjectIdUnderTwoUsersIsTwoSessions()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSession a = await OpenNewAsync(store);
        ProjectSession b = await OpenNewAsync(store, Users.Bob);
        Assert.NotSame(a, b);
        Assert.Equal(Users.Alice, a.Owner);
        Assert.Equal(Users.Bob, b.Owner);
    }

    private static async Task<ProjectId> CreateAsync(TestStore store, UserId user)
    {
        return ProjectId.From((await store.Service.CreateAsync(user, "Song", _none)).Id);
    }

    private static async Task<ProjectSession> OpenNewAsync(TestStore store, UserId? user = null)
    {
        UserId owner = user ?? Users.Alice;
        return await store.Open.OpenAsync(owner, await CreateAsync(store, owner), _none);
    }
}
