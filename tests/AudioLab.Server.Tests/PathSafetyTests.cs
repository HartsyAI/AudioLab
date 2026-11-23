using System.Text.RegularExpressions;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

public sealed partial class PathSafetyTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    public static TheoryData<string> HostileIds() =>
    [
        "..", ".", "../x", "..\\x", "a/b", "a\\b", "C:\\Windows", "C:", "/etc/passwd", "\\\\server\\share", "~", "CON", "nul", "com1",
        "LPT9", "A", "a.b", "a b", "a:b", "a%2fb", "a\u0000b", "", " ", new string('a', 65), "caf\u00e9", "a\nb", "prj_x\\..\\..",
    ];

    [Theory]
    [MemberData(nameof(HostileIds))]
    public void AnIdThatCouldNameAPathIsNotAnId(string hostile)
    {
        Assert.False(IdRules.IsValid(hostile));
        Assert.Throws<ArgumentException>(() => ProjectId.From(hostile));
        Assert.Throws<ArgumentException>(() => UserId.From(hostile));
        Assert.Throws<ArgumentException>(() => MediaId.From(hostile));
    }

    [Fact]
    public void ANameOnDiskIsAHashAndNoIdReachesAPath()
    {
        using TempFolder folder = new();
        StorePaths paths = new(Path.Combine(folder.Path, "store"));
        foreach (string text in new[] { "prj_secretmarker", "con-x", new string('z', 64), "x_y_z" })
        {
            string file = paths.ProjectFile(Users.Alice, ProjectId.From(text));
            Assert.StartsWith(paths.Root + Path.DirectorySeparatorChar, file, StringComparison.Ordinal);
            Assert.DoesNotContain(text, file[paths.Root.Length..], StringComparison.OrdinalIgnoreCase);
            Assert.Matches(ProjectFileName(), Path.GetFileName(file));
            Assert.True(StorePaths.IsRecordFileName(Path.GetFileName(file)));
            Assert.Matches(RelativeLayout(), file[(paths.Root.Length + 1)..].Replace('\\', '/'));
        }
    }

    [Fact]
    public void DifferentIdsAndKindsGetDifferentNames()
    {
        HashSet<string> names =
        [
            StorePaths.ProjectFileName(ProjectId.From("a")),
            StorePaths.ProjectFileName(ProjectId.From("b")),
            StorePaths.ProjectFileName(ProjectId.From("a-")),
            StorePaths.MediaFileName(MediaId.From("a")),
            StorePaths.MediaFileName(MediaId.From("b")),
        ];
        Assert.Equal(5, names.Count);
        Assert.Equal(StorePaths.ProjectFileName(ProjectId.From("a")), StorePaths.ProjectFileName(ProjectId.From("a")));
    }

    [Fact]
    public async Task NothingIsEverWrittenOutsideTheStoreRoot()
    {
        using TempFolder parent = new();
        string sentinel = Path.Combine(parent.Path, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "keep");
        string root = Path.Combine(parent.Path, "store");
        await using (TestStore store = TestStore.On(root))
        {
            foreach (UserId user in new[] { Users.Alice, Users.Bob, UserId.Local, UserId.ForDoor("swarm", "dotdot") })
            {
                ProjectSummary created = await store.Service.CreateAsync(user, "../../escape", _none);
                ProjectId id = ProjectId.From(created.Id);
                (await store.Open.OpenAsync(user, id, _none)).Update(document => document with { Name = "..\\..\\escape2" });
                await store.Service.SaveAsync(user, id, _none);
                await store.Service.DuplicateAsync(user, id, "..", _none);
                await store.UploadAsync(user, TestWav.Build(SampleEncoding.Pcm16, 1, 48000, 100), "../../escape.wav");
            }
        }

        Assert.Equal(["sentinel.txt", "store"], Directory.GetFileSystemEntries(parent.Path).Select(entry => Path.GetFileName(entry)!).Order().ToArray());
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            Assert.Matches(RelativeLayout(), relative);
        }
    }

    [Fact]
    public void ARootThatIsNotAbsoluteIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new StorePaths("relative/store"));
        Assert.Throws<InvalidOperationException>(() => new StoreOptions { Root = "relative/store" }.ResolveRoot());
    }

    [GeneratedRegex(@"\Ap[0-9a-f]{32}\.json\z")]
    private static partial Regex ProjectFileName();

    [GeneratedRegex(@"\Ausers/u[0-9a-f]{32}/(projects|media)/[pm][0-9a-f]{32}\.(json|wav|peaks)\z")]
    private static partial Regex RelativeLayout();
}
