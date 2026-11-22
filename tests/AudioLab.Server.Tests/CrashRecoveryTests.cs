using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

public sealed class CrashRecoveryTests
{
    public static TheoryData<AtomicWriteStage> Stages() => AtomicFileTests.Stages();

    [Theory]
    [MemberData(nameof(Stages))]
    public async Task AStoreStartedAfterASaveStoppedAtAnyStageReadsTheOldOrTheNewProjectAndSweepsTheLeftovers(AtomicWriteStage crashAt)
    {
        using TempFolder folder = new();
        ProjectId id;
        TestStore first = TestStore.On(folder.Path);
        try
        {
            id = ProjectId.From((await first.Service.CreateAsync(Users.Alice, "Song", CancellationToken.None)).Id);
            ProjectSession session = await first.Open.OpenAsync(Users.Alice, id, CancellationToken.None);
            session.Update(document => document with { Name = "Song, second take" });
            first.Observer = (stage, temp) =>
            {
                LeaveWhatACrashWouldLeave(stage, temp);
                if (stage == crashAt)
                {
                    throw new SimulatedCrash(stage.ToString());
                }
            };
            await Assert.ThrowsAsync<SimulatedCrash>(() => session.SaveAsync(CancellationToken.None));
            Assert.True(session.IsUnsaved);
        }
        finally
        {
            first.Crash();
        }

        await using TestStore second = TestStore.On(folder.Path);
        ProjectDocument reloaded = await second.Projects.ReadAsync(Users.Alice, id, CancellationToken.None);
        Assert.Equal(crashAt == AtomicWriteStage.Renamed ? "Song, second take" : "Song", reloaded.Name);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp", SearchOption.AllDirectories));
        ProjectListing listing = await second.Service.ListAsync(Users.Alice, CancellationToken.None);
        Assert.Single(listing.Projects);
        Assert.Equal(0, listing.Refused);
    }

    [Fact]
    public async Task ALeftoverTemporaryFileIsNeverListedOrLoadedAndIsSweptAtStart()
    {
        using TempFolder folder = new();
        string leftover;
        ProjectId id;
        TestStore first = TestStore.On(folder.Path);
        try
        {
            id = ProjectId.From((await first.Service.CreateAsync(Users.Alice, "Song", CancellationToken.None)).Id);
            string file = first.Paths.ProjectFile(Users.Alice, id);
            byte[] whole = await File.ReadAllBytesAsync(file);
            leftover = $"{file}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(leftover, whole[..(whole.Length / 2)]);
            string unrelated = Path.Combine(Path.GetDirectoryName(file)!, "notes.tmp");
            await File.WriteAllTextAsync(unrelated, "not ours");
            ProjectListing listing = await first.Service.ListAsync(Users.Alice, CancellationToken.None);
            Assert.Equal([id.Value], listing.Projects.Select(project => project.Id));
            Assert.Equal(0, listing.Refused);
        }
        finally
        {
            first.Crash();
        }

        await using TestStore second = TestStore.On(folder.Path);
        Assert.False(File.Exists(leftover));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(leftover)!, "notes.tmp")));
        Assert.Equal("Song", (await second.Projects.ReadAsync(Users.Alice, id, CancellationToken.None)).Name);
    }

    // The save is stopped by an exception and the process lives on, so this does not kill a process. What it does show is
    // the state a kill at that stage would leave: the observer leaves a temporary file behind as a crash would (a
    // half-written one before the file is closed, a complete copy after), and the next store must cope with it.
    private static void LeaveWhatACrashWouldLeave(AtomicWriteStage stage, string temp)
    {
        string leftover = $"{temp[..^(32 + ".tmp".Length + 1)]}.{Guid.NewGuid():N}.tmp";
        if (stage >= AtomicWriteStage.BeforeRename && File.Exists(temp))
        {
            File.Copy(temp, leftover);
        }
        else if (stage == AtomicWriteStage.PartlyWritten)
        {
            File.WriteAllText(leftover, "{\"version\":4,\"id\":\"prj_");
        }
    }

    [Fact]
    public async Task AStartThatCannotRemoveALeftoverTemporaryFileStillStarts()
    {
        using TempFolder folder = new();
        string leftover;
        ProjectId id;
        TestStore first = TestStore.On(folder.Path);
        try
        {
            id = ProjectId.From((await first.Service.CreateAsync(Users.Alice, "Song", CancellationToken.None)).Id);
            string file = first.Paths.ProjectFile(Users.Alice, id);
            leftover = $"{file}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(leftover, "{");
        }
        finally
        {
            first.Crash();
        }

        using (new FileStream(leftover, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await using TestStore second = TestStore.On(folder.Path);
            Assert.Equal("Song", (await second.Projects.ReadAsync(Users.Alice, id, CancellationToken.None)).Name);
        }
    }
}
