using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

public sealed class ProjectServiceTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    [Fact]
    public async Task ACreatedProjectIsOwnedByTheCallerAndReadsBackStrictly()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectSummary created = await store.Service.CreateAsync(Users.Alice, "First song", _none);
        ProjectDocument read = ProjectJson.Read(await File.ReadAllTextAsync(store.Paths.ProjectFile(Users.Alice, ProjectId.From(created.Id))));
        Assert.Equal(Users.Alice, read.Owner);
        Assert.Equal("First song", read.Name);
        Assert.Equal(ProjectDocument.CurrentVersion, read.Version);
        Assert.Empty(ProjectValidator.Validate(read));
        Assert.Equal([created.Id], (await store.Service.ListAsync(Users.Alice, _none)).Projects.Select(project => project.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("tab\there")]
    [InlineData("bidi‮text")]
    public async Task ANameThatBreaksTheRulesCreatesNothing(string name)
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectFormatException error = await Assert.ThrowsAsync<ProjectFormatException>(() => store.Service.CreateAsync(Users.Alice, name, _none));
        Assert.Contains(error.Problems, problem => problem.StartsWith("name", StringComparison.Ordinal));
        Assert.Empty((await store.Service.ListAsync(Users.Alice, _none)).Projects);
        Assert.Empty(Directory.GetFiles(folder.Path, "p*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ANameOverTheLimitCreatesNothing()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        await Assert.ThrowsAsync<ProjectFormatException>(() => store.Service.CreateAsync(Users.Alice, new string('x', ProjectLimits.MaxName + 1), _none));
        Assert.Empty((await store.Service.ListAsync(Users.Alice, _none)).Projects);
    }

    [Fact]
    public async Task SaveWritesTheUnsavedDocumentNow()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        ProjectSession session = await store.Open.OpenAsync(Users.Alice, id, _none);
        session.Update(document => document with { Bpm = 133 });
        Assert.Equal(120, (await store.Projects.ReadAsync(Users.Alice, id, _none)).Bpm);
        Assert.Equal(2, (await store.Service.SaveAsync(Users.Alice, id, _none)).Revision);
        Assert.Equal(133, (await store.Projects.ReadAsync(Users.Alice, id, _none)).Bpm);
        Assert.False(session.IsUnsaved);
    }

    [Fact]
    public async Task DeleteRemovesTheProjectEverywhereAndASecondDeleteFindsNothing()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        await store.Service.OpenAsync(Users.Alice, id, _none);
        await store.Service.DeleteAsync(Users.Alice, id, _none);
        Assert.False(File.Exists(store.Paths.ProjectFile(Users.Alice, id)));
        Assert.Empty((await store.Service.ListAsync(Users.Alice, _none)).Projects);
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Service.OpenAsync(Users.Alice, id, _none));
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Service.DeleteAsync(Users.Alice, id, _none));
    }

    [Fact]
    public async Task DuplicateMakesAnIndependentProjectFromTheOpenDocumentIncludingUnsavedChanges()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        ProjectSession original = await store.Open.OpenAsync(Users.Alice, id, _none);
        original.Update(document => document with { Bpm = 140 });

        ProjectSummary copy = await store.Service.DuplicateAsync(Users.Alice, id, null, _none);
        ProjectId copyId = ProjectId.From(copy.Id);
        Assert.NotEqual(id, copyId);
        Assert.Equal("Copy of Song", copy.Name);
        ProjectDocument copied = await store.Projects.ReadAsync(Users.Alice, copyId, _none);
        Assert.Equal(140, copied.Bpm);
        Assert.Equal(Users.Alice, copied.Owner);

        (await store.Open.OpenAsync(Users.Alice, copyId, _none)).Update(document => document with { Bpm = 70 });
        Assert.Equal(140, original.Snapshot.Document.Bpm);
        Assert.Equal(2, (await store.Service.ListAsync(Users.Alice, _none)).Projects.Count);
        Assert.Equal("Named", (await store.Service.DuplicateAsync(Users.Alice, id, "Named", _none)).Name);
    }

    [Fact]
    public async Task ACopyOfAVeryLongNameIsCutToTheLimitWithoutSplittingACharacter()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        string name = new string('x', ProjectLimits.MaxName - 9) + "\U0001F600";
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, name, _none)).Id);
        ProjectSummary copy = await store.Service.DuplicateAsync(Users.Alice, id, null, _none);
        Assert.Equal("Copy of " + new string('x', ProjectLimits.MaxName - 9), copy.Name);
        Assert.Empty(ProjectValidator.Validate(await store.Projects.ReadAsync(Users.Alice, ProjectId.From(copy.Id), _none)));
    }

    [Fact]
    public async Task TheListIsNewestFirstAndAnOpenProjectShowsItsCurrentName()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId first = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "First", _none)).Id);
        await Task.Delay(20);
        ProjectId second = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Second", _none)).Id);
        ProjectSession session = await store.Open.OpenAsync(Users.Alice, first, _none);
        store.Time.Advance(TimeSpan.FromHours(1));
        session.Update(document => document with { Name = "First, renamed" });
        ProjectListing listing = await store.Service.ListAsync(Users.Alice, _none);
        Assert.Equal(["First, renamed", "Second"], listing.Projects.Select(project => project.Name));
        Assert.Equal([first.Value, second.Value], listing.Projects.Select(project => project.Id));
    }

    [Fact]
    public async Task ARestartKeepsTheProjectsAndMediaOfEveryUser()
    {
        using TempFolder folder = new();
        string aliceProject;
        string bobProject;
        MediaRecord media;
        await using (TestStore first = TestStore.On(folder.Path))
        {
            aliceProject = (await first.Service.CreateAsync(Users.Alice, "A", _none)).Id;
            bobProject = (await first.Service.CreateAsync(Users.Bob, "B", _none)).Id;
            media = await first.Media.CreateAsync(Users.Alice, "audio/flac", 99, _none);
        }
        await using TestStore second = TestStore.On(folder.Path);
        Assert.Equal([aliceProject], (await second.Service.ListAsync(Users.Alice, _none)).Projects.Select(project => project.Id));
        Assert.Equal([bobProject], (await second.Service.ListAsync(Users.Bob, _none)).Projects.Select(project => project.Id));
        Assert.Equal(media, await second.Media.GetAsync(Users.Alice, media.Id, _none));
    }

    [Fact]
    public async Task MediaRecordsCreateReadListAndDelete()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.Media.CreateAsync(Users.Alice, "audio/wav", 4096, _none);
        Assert.Equal(new MediaRecord { Id = record.Id, Owner = Users.Alice, ContentType = "audio/wav", Length = 4096 }, record);
        Assert.Equal([record], (await store.Media.ListAsync(Users.Alice, _none)).Records);
        Assert.True(store.Media.Delete(Users.Alice, record.Id));
        Assert.False(store.Media.Delete(Users.Alice, record.Id));
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Media.GetAsync(Users.Alice, record.Id, _none));
    }

    [Theory]
    [InlineData("AUDIO/WAV", 10)]
    [InlineData("audio/wav; charset=x", 10)]
    [InlineData("audio", 10)]
    [InlineData("audio/wav", 0)]
    [InlineData("audio/wav", -1)]
    public async Task ARecordThatBreaksTheMediaRulesIsNotStored(string contentType, long length)
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        await Assert.ThrowsAsync<ProjectFormatException>(() => store.Media.CreateAsync(Users.Alice, contentType, length, _none));
        Assert.Empty((await store.Media.ListAsync(Users.Alice, _none)).Records);
    }

    [Fact]
    public async Task ASecondProcessCannotUseTheSameStoreFolder()
    {
        using TempFolder folder = new();
        await using TestStore first = TestStore.On(folder.Path);
        Assert.Throws<StoreLockedException>(() => TestStore.On(folder.Path));
        await first.DisposeAsync();
        await using TestStore again = TestStore.On(folder.Path);
        Assert.NotNull(again.Root);
    }
}
