using System.Text;
using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

public sealed class CorruptionTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    // The list reads only the start of a file, so a file whose start is intact is listed and refuses to open.
    public static TheoryData<string, string, bool> Corruptions() => new()
    {
        { "truncated", "truncate", true },
        { "empty", "empty", false },
        { "not an object", "array", false },
        { "wrong version", "version", true },
        { "rule broken", "bpm", true },
        { "unknown member", "unknown", true },
        { "not utf-8", "bytes", false },
        { "too large", "large", true },
    };

    [Theory]
    [MemberData(nameof(Corruptions))]
    public async Task ACorruptFileIsRefusedWithItsErrorListAndNeverLoaded(string label, string kind, bool listed)
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, new StoreOptions { MaxFileBytes = 64 * 1024 });
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        string file = store.Paths.ProjectFile(Users.Alice, id);
        byte[] corrupt = Corrupt(await File.ReadAllTextAsync(file), kind);
        await File.WriteAllBytesAsync(file, corrupt);

        ProjectFormatException error = await Assert.ThrowsAsync<ProjectFormatException>(() => store.Service.OpenAsync(Users.Alice, id, _none));
        Assert.NotEmpty(error.Problems);
        Assert.True(error.Problems.All(problem => problem.Length <= ProjectLimits.MaxProblemLength + 3), label);
        await Assert.ThrowsAsync<ProjectFormatException>(() => store.Service.OpenAsync(Users.Alice, id, _none));
        ProjectListing listing = await store.Service.ListAsync(Users.Alice, _none);
        Assert.Equal(listed ? 1 : 0, listing.Projects.Count);
        Assert.Equal(listed ? 0 : 1, listing.Refused);
        Assert.Equal(corrupt, await File.ReadAllBytesAsync(file));
        Assert.Empty(store.Open.SessionsOf(Users.Alice));
    }

    [Fact]
    public async Task ARuleBreakNamesTheValueThatBreaksIt()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        string file = store.Paths.ProjectFile(Users.Alice, id);
        string json = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(file, json.Replace("\"bpm\":120", "\"bpm\":0", StringComparison.Ordinal).Replace("\"masterVolume\":1,", "\"masterVolume\":-3,", StringComparison.Ordinal));

        ProjectFormatException error = await Assert.ThrowsAsync<ProjectFormatException>(() => store.Service.OpenAsync(Users.Alice, id, _none));
        Assert.Contains(error.Problems, problem => problem.StartsWith("bpm", StringComparison.Ordinal));
        Assert.Contains(error.Problems, problem => problem.StartsWith("masterVolume", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AWrongVersionIsNamedAsSuch()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        string file = store.Paths.ProjectFile(Users.Alice, id);
        string json = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(file, json.Replace("\"version\":4", "\"version\":5", StringComparison.Ordinal));
        ProjectFormatException error = await Assert.ThrowsAsync<ProjectFormatException>(() => store.Service.OpenAsync(Users.Alice, id, _none));
        Assert.Contains(error.Problems, problem => problem.StartsWith("version", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AGoodProjectBesideACorruptOneStillListsAndOpens()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId bad = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Bad", _none)).Id);
        ProjectId good = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Good", _none)).Id);
        await File.WriteAllTextAsync(store.Paths.ProjectFile(Users.Alice, bad), "{");
        ProjectListing listing = await store.Service.ListAsync(Users.Alice, _none);
        Assert.Equal([good.Value], listing.Projects.Select(project => project.Id));
        Assert.Equal(1, listing.Refused);
        Assert.Equal("Good", (await store.Service.OpenAsync(Users.Alice, good, _none)).Document.Name);
    }

    [Fact]
    public async Task ASessionThatAlreadyHasTheProjectIsNotAffectedByLaterDamageToItsFile()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        ProjectId id = ProjectId.From((await store.Service.CreateAsync(Users.Alice, "Song", _none)).Id);
        await store.Service.OpenAsync(Users.Alice, id, _none);
        await File.WriteAllTextAsync(store.Paths.ProjectFile(Users.Alice, id), "damaged");
        ProjectListing listing = await store.Service.ListAsync(Users.Alice, _none);
        Assert.Equal([id.Value], listing.Projects.Select(project => project.Id));
        Assert.Equal(0, listing.Refused);
        Assert.Equal("Song", (await store.Service.OpenAsync(Users.Alice, id, _none)).Document.Name);
        await store.Service.SaveAsync(Users.Alice, id, _none);
    }

    private static byte[] Corrupt(string json, string kind)
    {
        return kind switch
        {
            "truncate" => Encoding.UTF8.GetBytes(json[..(json.Length / 2)]),
            "empty" => [],
            "array" => Encoding.UTF8.GetBytes("[]"),
            "version" => Encoding.UTF8.GetBytes(json.Replace("\"version\":4", "\"version\":3", StringComparison.Ordinal)),
            "bpm" => Encoding.UTF8.GetBytes(json.Replace("\"bpm\":120", "\"bpm\":0", StringComparison.Ordinal)),
            "unknown" => Encoding.UTF8.GetBytes(json.Replace("\"version\":4", "\"future\":true,\"version\":4", StringComparison.Ordinal)),
            "bytes" => [.. Encoding.UTF8.GetBytes(json[..20]), 0xFF, 0xFE, 0xFD],
            "large" => Encoding.UTF8.GetBytes(json + new string(' ', 70 * 1024)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
