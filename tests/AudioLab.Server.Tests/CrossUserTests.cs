using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

public sealed class CrossUserTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    [Fact]
    public async Task AnotherUserCannotSeeOpenSaveDuplicateOrDeleteAProject()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Alice's song", _none)).Id);

        Assert.Empty((await store.Service.ListAsync(Users.Bob, _none)).Projects);
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Service.OpenAsync(Users.Bob, id, _none));
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Service.SaveAsync(Users.Bob, id, _none));
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Service.DuplicateAsync(Users.Bob, id, null, _none));
        await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Service.DeleteAsync(Users.Bob, id, _none));

        ProjectDocument still = (await store.Service.OpenAsync(Users.Alice, id, _none)).Document;
        Assert.Equal("Alice's song", still.Name);
        Assert.Equal(Users.Alice, still.Owner);
        Assert.Single((await store.Service.ListAsync(Users.Alice, _none)).Projects);
        Assert.Empty((await store.Service.ListAsync(Users.Bob, _none)).Projects);
    }

    [Fact]
    public async Task AProjectIsOwnedByTheUserWhoCreatedItWhateverTheFileSays()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Alice's song", _none)).Id);
        Assert.Equal(Users.Alice, (await store.Projects.ReadAsync(Users.Alice, id, _none)).Owner);
    }

    [Fact]
    public async Task AFileThatNamesAnotherOwnerIsRefusedForTheUserItIsStoredUnder()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Alice's song", _none)).Id);
        string alices = store.Paths.ProjectFile(Users.Alice, id);
        string bobs = store.Paths.ProjectFile(Users.Bob, id);
        Directory.CreateDirectory(Path.GetDirectoryName(bobs)!);
        File.Copy(alices, bobs);

        ProjectFormatException error = await Assert.ThrowsAsync<ProjectFormatException>(() => store.Service.OpenAsync(Users.Bob, id, _none));
        Assert.Contains(error.Problems, problem => problem.StartsWith("owner", StringComparison.Ordinal));
        ProjectListing listing = await store.Service.ListAsync(Users.Bob, _none);
        Assert.Empty(listing.Projects);
        Assert.Equal(1, listing.Refused);
        Assert.Equal(Users.Alice, (await store.Service.OpenAsync(Users.Alice, id, _none)).Document.Owner);
    }

    [Fact]
    public async Task AFileThatBelongsToTheUserButIsStoredUnderAnotherProjectsNameIsRefused()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        ProjectId other = ProjectId.From("prj_other");
        File.Copy(store.Paths.ProjectFile(Users.Alice, id), store.Paths.ProjectFile(Users.Alice, other));

        ProjectFormatException error = await Assert.ThrowsAsync<ProjectFormatException>(() => store.Service.OpenAsync(Users.Alice, other, _none));
        Assert.Contains(error.Problems, problem => problem.StartsWith("id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MediaRecordsAreScopedToTheirOwnerToo()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.Media.CreateAsync(Users.Alice, "audio/wav", 1234, _none);
        Assert.Equal(Users.Alice, record.Owner);

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => store.Media.GetAsync(Users.Bob, record.Id, _none));
        Assert.Empty((await store.Media.ListAsync(Users.Bob, _none)).Records);
        Assert.False(store.Media.Delete(Users.Bob, record.Id));
        Assert.Equal(record, await store.Media.GetAsync(Users.Alice, record.Id, _none));

        string bobs = store.Paths.MediaRecordFile(Users.Bob, record.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(bobs)!);
        File.Copy(store.Paths.MediaRecordFile(Users.Alice, record.Id), bobs);
        await Assert.ThrowsAsync<ProjectFormatException>(() => store.Media.GetAsync(Users.Bob, record.Id, _none));
        (IReadOnlyList<MediaRecord> records, int refused) = await store.Media.ListAsync(Users.Bob, _none);
        Assert.Empty(records);
        Assert.Equal(1, refused);
    }
}
