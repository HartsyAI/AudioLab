using System.Text;
using AudioLab.Server.Store;

namespace AudioLab.Server.Tests;

public sealed class AtomicFileTests
{
    private static readonly byte[] _old = Encoding.UTF8.GetBytes(new string('o', 1001));

    private static readonly byte[] _new = Encoding.UTF8.GetBytes(new string('n', 2003));

    public static TheoryData<AtomicWriteStage> Stages()
    {
        TheoryData<AtomicWriteStage> data = [];
        foreach (AtomicWriteStage stage in Enum.GetValues<AtomicWriteStage>())
        {
            data.Add(stage);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Stages))]
    public async Task AWriteThatFailsAtAnyStageLeavesTheWholeOldOrTheWholeNewContentAndNoTemporaryFile(AtomicWriteStage crashAt)
    {
        using TempFolder folder = new();
        string path = Path.Combine(folder.Path, "file.json");
        await File.WriteAllBytesAsync(path, _old);
        List<string> seen = [];
        AtomicFile writer = new(null, (stage, temp) =>
        {
            seen.Add(stage.ToString());
            byte[] atThisPoint = File.ReadAllBytes(path);
            Assert.Equal(stage == AtomicWriteStage.Renamed ? _new : _old, atThisPoint);
            if (stage == crashAt)
            {
                throw new SimulatedCrash(stage.ToString());
            }
        });

        await Assert.ThrowsAsync<SimulatedCrash>(() => writer.WriteAsync(path, _new, CancellationToken.None));

        Assert.Equal(crashAt == AtomicWriteStage.Renamed ? _new : _old, await File.ReadAllBytesAsync(path));
        Assert.Equal(Enum.GetValues<AtomicWriteStage>().TakeWhile(stage => stage <= crashAt).Select(stage => stage.ToString()), seen);
        Assert.Equal([path], Directory.GetFiles(folder.Path));
    }

    [Theory]
    [MemberData(nameof(Stages))]
    public async Task AFirstWriteThatFailsAtAnyStageLeavesNoDestinationUntilTheRename(AtomicWriteStage crashAt)
    {
        using TempFolder folder = new();
        string path = Path.Combine(folder.Path, "file.json");
        AtomicFile writer = new(null, (stage, temp) =>
        {
            Assert.Equal(stage == AtomicWriteStage.Renamed, File.Exists(path));
            if (stage == crashAt)
            {
                throw new SimulatedCrash(stage.ToString());
            }
        });

        await Assert.ThrowsAsync<SimulatedCrash>(() => writer.WriteAsync(path, _new, CancellationToken.None));

        Assert.Equal(crashAt == AtomicWriteStage.Renamed, File.Exists(path));
    }

    [Fact]
    public async Task TheTemporaryFileHoldsTheContentOnceItIsClosed()
    {
        using TempFolder folder = new();
        string path = Path.Combine(folder.Path, "file.json");
        byte[]? closed = null;
        AtomicFile writer = new(null, (stage, temp) =>
        {
            if (stage == AtomicWriteStage.BeforeRename)
            {
                closed = File.ReadAllBytes(temp);
            }
        });
        await writer.WriteAsync(path, _new, CancellationToken.None);
        Assert.Equal(_new, closed);
        Assert.Equal(_new, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task ACancelledWriteLeavesTheOldContentAndNoTemporaryFile()
    {
        using TempFolder folder = new();
        string path = Path.Combine(folder.Path, "file.json");
        await File.WriteAllBytesAsync(path, _old);
        using CancellationTokenSource cancel = new();
        AtomicFile writer = new(null, (stage, temp) =>
        {
            if (stage == AtomicWriteStage.PartlyWritten)
            {
                cancel.Cancel();
            }
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(path, _new, cancel.Token));
        Assert.Equal(_old, await File.ReadAllBytesAsync(path));
        Assert.Equal([path], Directory.GetFiles(folder.Path));
    }

    [Fact]
    public async Task AReaderNeverSeesAHalfWrittenFileWhileAWriterReplacesIt()
    {
        using TempFolder folder = new();
        string path = Path.Combine(folder.Path, "file.json");
        byte[] a = Encoding.UTF8.GetBytes(new string('a', 300_000));
        byte[] b = Encoding.UTF8.GetBytes(new string('b', 300_001));
        await File.WriteAllBytesAsync(path, a);
        AtomicFile writer = new();
        bool done = false;
        int reads = 0;
        Task reader = Task.Run(async () =>
        {
            while (!Volatile.Read(ref done))
            {
                byte[] seen;
                try
                {
                    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    seen = new byte[stream.Length];
                    stream.ReadExactly(seen);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                Assert.True(seen.SequenceEqual(a) || seen.SequenceEqual(b), "a reader saw content that is neither version");
                reads++;
                await Task.Delay(2);
            }
        });
        for (int i = 0; i < 60; i++)
        {
            await writer.WriteAsync(path, i % 2 == 0 ? b : a, CancellationToken.None);
        }
        Volatile.Write(ref done, true);
        await reader;
        Assert.True(reads > 0);
    }
}
