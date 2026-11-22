using System.Security.Cryptography;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Media;
using AudioLab.Server.Store;
using static AudioLab.Server.Tests.MediaTestSupport;

namespace AudioLab.Server.Tests;

public sealed class MediaStoreTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    private static Task<MediaRecord> Send(TestStore store, Stream body, long? declared, string name = "a.wav")
    {
        return store.MediaFiles.UploadAsync(Users.Alice, name, body, declared, _none);
    }

    private static string[] MediaFiles(TestStore store, UserId user)
    {
        string directory = store.Paths.MediaDirectory(user);
        return Directory.Exists(directory) ? [.. Directory.EnumerateFiles(directory).Select(file => Path.GetFileName(file))] : [];
    }

    [Fact]
    public async Task AnUploadRecordsWhatWasMeasuredFromTheBytesAndKeepsThemIntact()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        byte[] file = Wav();

        MediaRecord record = await store.UploadAsync(Users.Alice, file, "Kick.wav");

        Assert.Equal(Users.Alice, record.Owner);
        Assert.Equal("Kick.wav", record.Name);
        Assert.Equal(MediaKind.Audio, record.Kind);
        Assert.Equal(MediaFormat.Wav, record.Format);
        Assert.Equal(SampleEncoding.Pcm16, record.Encoding);
        Assert.Equal(0.5, record.Duration);
        Assert.Equal(48000, record.SampleRate);
        Assert.Equal(2, record.Channels);
        Assert.Equal(file.Length, record.Bytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(file)), record.Sha256);
        Assert.Equal(store.Time.GetUtcNow(), record.Created);
        Assert.Equal(file, await ReadAllAsync(store.MediaFiles.OpenContent(Users.Alice, record.Id)));
        Assert.Equal(record, store.Media.Find(Users.Alice, record.Id));
    }

    [Fact]
    public async Task AFileIsOnlyEverSeenByItsOwnerAndAMissingOneLooksTheSame()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());

        Assert.Null(store.Media.Find(Users.Bob, record.Id));
        MediaNotFoundException other = Assert.Throws<MediaNotFoundException>(() => store.MediaFiles.Get(Users.Bob, record.Id));
        MediaNotFoundException missing = Assert.Throws<MediaNotFoundException>(() => store.MediaFiles.Get(Users.Bob, MediaId.From("med_nothing")));
        Assert.Equal(missing.Message, other.Message);
        Assert.Throws<MediaNotFoundException>(() => store.MediaFiles.OpenContent(Users.Bob, record.Id));
        await Assert.ThrowsAsync<MediaNotFoundException>(() => store.MediaFiles.DeleteAsync(Users.Bob, record.Id, _none));
        Assert.Empty(store.MediaFiles.List(Users.Bob).Files);
        Assert.Equal([record.Id.Value], store.MediaFiles.List(Users.Alice).Files.Select(summary => summary.Id));
        Assert.NotNull(store.Media.Find(Users.Alice, record.Id));
    }

    [Fact]
    public async Task ARecordCopiedIntoAnotherUsersFolderIsRefusedThere()
    {
        using TempFolder folder = new();
        MediaRecord record;
        await using (TestStore first = TestStore.On(folder.Path))
        {
            record = await first.UploadAsync(Users.Alice, Wav());
            string bobs = first.Paths.MediaDirectory(Users.Bob);
            Directory.CreateDirectory(bobs);
            File.Copy(first.Paths.MediaRecordFile(Users.Alice, record.Id), first.Paths.MediaRecordFile(Users.Bob, record.Id));
            File.Copy(first.Paths.MediaDataFile(Users.Alice, record.Id), first.Paths.MediaDataFile(Users.Bob, record.Id));
        }
        await using TestStore store = TestStore.On(folder.Path);
        Assert.Null(store.Media.Find(Users.Bob, record.Id));
        Assert.Equal(1, store.MediaFiles.List(Users.Bob).Refused);
        Assert.Empty(store.MediaFiles.List(Users.Bob).Files);
        Assert.NotNull(store.Media.Find(Users.Alice, record.Id));
    }

    [Fact]
    public async Task AFileSurvivesARestartAndABrokenRecordOrMissingBytesAreRefusedAfterIt()
    {
        using TempFolder folder = new();
        MediaRecord kept;
        MediaRecord damaged;
        MediaRecord truncated;
        MediaRecord lost;
        await using (TestStore first = TestStore.On(folder.Path))
        {
            kept = await first.UploadAsync(Users.Alice, Wav(), "kept.wav");
            damaged = await first.UploadAsync(Users.Alice, Wav(), "damaged.wav");
            truncated = await first.UploadAsync(Users.Alice, Wav(), "truncated.wav");
            lost = await first.UploadAsync(Users.Alice, Wav(), "lost.wav");
            string record = first.Paths.MediaRecordFile(Users.Alice, damaged.Id);
            await File.WriteAllTextAsync(record, (await File.ReadAllTextAsync(record)).Replace("\"pcm16\"", "\"PCM16\"", StringComparison.Ordinal));
            await using (FileStream stream = new(first.Paths.MediaDataFile(Users.Alice, truncated.Id), FileMode.Open, FileAccess.Write))
            {
                stream.SetLength(stream.Length - 4);
            }
            File.Delete(first.Paths.MediaDataFile(Users.Alice, lost.Id));
        }

        await using TestStore store = TestStore.On(folder.Path);
        Assert.Equal(kept, store.Media.Find(Users.Alice, kept.Id));
        Assert.Null(store.Media.Find(Users.Alice, damaged.Id));
        Assert.Null(store.Media.Find(Users.Alice, truncated.Id));
        Assert.Null(store.Media.Find(Users.Alice, lost.Id));
        MediaListing listing = store.MediaFiles.List(Users.Alice);
        Assert.Equal([kept.Id.Value], listing.Files.Select(summary => summary.Id));
        Assert.Equal(3, listing.Refused);
    }

    [Fact]
    public async Task AFileThatIsNotAcceptedLeavesNothingBehind()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        byte[] good = Wav();
        byte[][] bad =
        [
            good[..^7],
            [.. good, 1],
            TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 10, extensible: false)[..30],
            "RIFF....WAVEfmt "u8.ToArray(),
            [1, 2, 3, 4],
            TestWav.Build(SampleEncoding.Float32, 1, 48000, 100, (frame, _) => frame == 50 ? double.NaN : 0),
        ];
        foreach (byte[] file in bad)
        {
            await Assert.ThrowsAsync<MediaRejectedException>(() => store.UploadAsync(Users.Alice, file));
        }
        await Assert.ThrowsAsync<MediaRejectedException>(() => Send(store, new MemoryStream(), null, "empty.wav"));

        Assert.Empty(MediaFiles(store, Users.Alice));
        Assert.Empty(store.MediaFiles.List(Users.Alice).Files);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("bad\nname.wav")]
    [InlineData("bad‮name.wav")]
    public async Task AnUnusableNameIsRefusedBeforeAnythingIsRead(string? name)
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        await Assert.ThrowsAsync<MediaRejectedException>(() => store.MediaFiles.UploadAsync(Users.Alice, name, new ThrowingStream(), null, _none));
        string tooLong = new('n', ProjectLimits.MaxName + 1);
        await Assert.ThrowsAsync<MediaRejectedException>(() => store.MediaFiles.UploadAsync(Users.Alice, tooLong, new ThrowingStream(), null, _none));
    }

    [Fact]
    public async Task ANameThatLooksLikeAPathIsOnlyALabelAndNeverReachesTheDisk()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav(), "..\\..\\..\\etc/passwd");
        Assert.Equal("..\\..\\..\\etc/passwd", record.Name);
        Assert.All(MediaFiles(store, Users.Alice), name => Assert.Matches("^m[0-9a-f]{32}\\.(json|wav)$", name));
    }

    [Fact]
    public async Task TheSizeLimitIsCountedOnTheBytesThatArriveNotOnWhatTheClientAnnounced()
    {
        byte[] file = Wav(100);
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, media: new MediaOptions { MaxUploadBytes = file.Length });

        MediaRecord exact = await store.UploadAsync(Users.Alice, file);
        Assert.Equal(file.Length, exact.Bytes);

        // Announces nothing, as a chunked body does, and sends one byte too many.
        await Assert.ThrowsAsync<MediaTooLargeException>(() => Send(store, new MemoryStream([.. file, 0]), null));
        // Announces less than it sends.
        await Assert.ThrowsAsync<MediaTooLargeException>(() => Send(store, new MemoryStream([.. file, 0]), 10));
        // Announces more than the limit: refused without reading a byte.
        await Assert.ThrowsAsync<MediaTooLargeException>(() => Send(store, new ThrowingStream(), file.Length + 1L));

        Assert.Equal(2, MediaFiles(store, Users.Alice).Length);
        Assert.Single(store.MediaFiles.List(Users.Alice).Files);
    }

    [Fact]
    public async Task TheQuotaStopsAFullUserAndOnlyThatUser()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, media: new MediaOptions { MaxFilesPerUser = 2 });
        await store.UploadAsync(Users.Alice, Wav());
        await store.UploadAsync(Users.Alice, Wav());
        await Assert.ThrowsAsync<MediaQuotaExceededException>(() => store.UploadAsync(Users.Alice, Wav()));
        await Assert.ThrowsAsync<MediaQuotaExceededException>(() => Send(store, new ThrowingStream(), 10));
        await store.UploadAsync(Users.Bob, Wav());
        Assert.Equal(4, MediaFiles(store, Users.Alice).Length);

        byte[] file = Wav();
        MediaOptions sizedOptions = new() { MaxBytesPerUser = (file.Length * 2L) + 10, MaxUploadBytes = file.Length };
        await using TestStore sized = TestStore.On(Path.Combine(folder.Path, "sized"), media: sizedOptions);
        await sized.UploadAsync(Users.Alice, file);
        await sized.UploadAsync(Users.Alice, file);
        await Assert.ThrowsAsync<MediaQuotaExceededException>(() => sized.UploadAsync(Users.Alice, file));
    }

    [Fact]
    public async Task ConcurrentUploadsOfTheSameContentAreSeparateFilesWithOneHashAndNeitherCorruptsTheOther()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        byte[] file = Wav(48000);

        Task<MediaRecord> Upload(int index) => Task.Run(() => store.UploadAsync(Users.Alice, file, $"same-{index}.wav"));
        Task<MediaRecord>[] uploads = [.. Enumerable.Range(0, 12).Select(Upload)];
        MediaRecord[] records = await Task.WhenAll(uploads);

        Assert.Equal(12, records.Select(record => record.Id).Distinct().Count());
        Assert.Single(records.Select(record => record.Sha256).Distinct());
        foreach (MediaRecord record in records)
        {
            Assert.Equal(file, await ReadAllAsync(store.MediaFiles.OpenContent(Users.Alice, record.Id)));
        }
        Assert.Equal(12, store.MediaFiles.List(Users.Alice).Files.Count);
        Assert.Equal(24, MediaFiles(store, Users.Alice).Length);
        Assert.DoesNotContain(MediaFiles(store, Users.Alice), name => name.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConcurrentUploadsCannotPassTheQuotaBetweenThem()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, media: new MediaOptions { MaxFilesPerUser = 5 });
        byte[] file = Wav(480);

        Task<MediaRecord>[] uploads = [.. Enumerable.Range(0, 14).Select(_ => Task.Run(() => store.UploadAsync(Users.Alice, file)))];
        await Task.WhenAll(uploads.Select(async upload =>
        {
            try
            {
                await upload;
            }
            catch (MediaQuotaExceededException)
            {
                // Expected for those that lose the race.
            }
        }));

        Assert.Equal(5, uploads.Count(upload => upload.IsCompletedSuccessfully));
        Assert.Equal(9, uploads.Count(upload => upload.IsFaulted));
        Assert.Equal(5, store.MediaFiles.List(Users.Alice).Files.Count);
        Assert.Equal(10, MediaFiles(store, Users.Alice).Length);
    }

    [Fact]
    public async Task AFailedWriteOfTheRecordLeavesNoBytesAndNoQuotaUsed()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, media: new MediaOptions { MaxFilesPerUser = 1 });
        store.Observer = (stage, _) =>
        {
            if (stage == AtomicWriteStage.Flushed)
            {
                throw new SimulatedCrash(stage.ToString());
            }
        };
        await Assert.ThrowsAsync<SimulatedCrash>(() => store.UploadAsync(Users.Alice, Wav()));
        store.Observer = null;

        Assert.Empty(MediaFiles(store, Users.Alice));
        Assert.Empty(store.MediaFiles.List(Users.Alice).Files);
        await store.UploadAsync(Users.Alice, Wav());
    }

    [Fact]
    public async Task ACrashBetweenTheBytesAndTheRecordIsCleanedUpAtTheNextStart()
    {
        using TempFolder folder = new();
        string orphan;
        string temp;
        MediaRecord kept;
        await using (TestStore first = TestStore.On(folder.Path))
        {
            kept = await first.UploadAsync(Users.Alice, Wav());
            MediaId stray = MediaId.From("med_stray");
            orphan = first.Paths.MediaDataFile(Users.Alice, stray);
            temp = first.Paths.MediaTempFile(Users.Alice, stray);
            await File.WriteAllBytesAsync(orphan, Wav());
            await File.WriteAllBytesAsync(temp, Wav());
        }

        await using TestStore store = TestStore.On(folder.Path);
        Assert.False(File.Exists(orphan));
        Assert.False(File.Exists(temp));
        Assert.Equal(kept, store.Media.Find(Users.Alice, kept.Id));
    }

    [Fact]
    public async Task ADeleteRemovesTheRecordAndTheBytesAndIsOnlyPossibleOnce()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());

        await store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none);

        Assert.Empty(MediaFiles(store, Users.Alice));
        Assert.Null(store.Media.Find(Users.Alice, record.Id));
        await Assert.ThrowsAsync<MediaNotFoundException>(() => store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none));
    }

    [Fact]
    public async Task ADownloadInProgressKeepsItsBytesWhileTheFileIsDeleted()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        byte[] file = Wav();
        MediaRecord record = await store.UploadAsync(Users.Alice, file);

        await using FileStream reader = store.MediaFiles.OpenContent(Users.Alice, record.Id);
        byte[] first = new byte[100];
        await reader.ReadExactlyAsync(first, _none);
        await store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none);
        byte[] rest = new byte[file.Length - 100];
        await reader.ReadExactlyAsync(rest, _none);

        Assert.True(file.AsSpan().SequenceEqual([.. first, .. rest]));
        Assert.Throws<MediaNotFoundException>(() => store.MediaFiles.OpenContent(Users.Alice, record.Id));
    }

    [Fact]
    public async Task AFileBeingDeletedCannotBeNamedByAnyoneUntilTheDeleteIsRefused()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());

        Assert.NotNull(store.Media.BeginDelete(Users.Alice, record.Id));
        Assert.Null(store.Media.Find(Users.Alice, record.Id));
        Assert.Null(store.Media.BeginDelete(Users.Alice, record.Id));
        store.Media.CancelDelete(Users.Alice, record.Id);
        Assert.Equal(record, store.Media.Find(Users.Alice, record.Id));
    }

    [Fact]
    public async Task AFileAProjectPlaysCannotBeDeletedWhateverHoldsTheReference()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: CoreCommands.CreateRegistry());
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        ProjectId project = await NewProjectAsync(store, Users.Alice, "Playing it");
        string clip = await AddClipAsync(store, Users.Alice, project, record);

        // In the open project's present.
        MediaInUseException used = await Assert.ThrowsAsync<MediaInUseException>(() => store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none));
        Assert.Contains("Playing it", used.Message, StringComparison.Ordinal);
        Assert.Equal(record, store.Media.Find(Users.Alice, record.Id));

        // Only in its undo history: the clip is gone from the present but undo can bring it back.
        Assert.True((await ApplyAsync(store, Users.Alice, project, "clip_delete", $"\"clip\":\"{clip}\"")).Succeeded);
        await Assert.ThrowsAsync<MediaInUseException>(() => store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none));

        // In a stored project that is not open.
        Assert.True((await store.Edits.UndoAsync(Users.Alice, project, _none)).Succeeded);
        await store.Service.SaveAsync(Users.Alice, project, _none);
        await store.Open.CloseAsync(Users.Alice, project, _none);
        await Assert.ThrowsAsync<MediaInUseException>(() => store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none));

        // Once nothing holds it, in the present, the history or the file, it goes.
        await store.Open.OpenAsync(Users.Alice, project, _none);
        Assert.True((await ApplyAsync(store, Users.Alice, project, "clip_delete", $"\"clip\":\"{clip}\"")).Succeeded);
        await store.Service.SaveAsync(Users.Alice, project, _none);
        await store.Open.CloseAsync(Users.Alice, project, _none);
        await store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none);
        Assert.Null(store.Media.Find(Users.Alice, record.Id));
    }

    [Fact]
    public async Task AProjectFileThatCannotBeReadDoesNotBlockADeleteOfAnotherFile()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: CoreCommands.CreateRegistry());
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        ProjectId broken = await NewProjectAsync(store, Users.Alice, "Broken");
        await store.Open.CloseAsync(Users.Alice, broken, _none);
        await File.WriteAllTextAsync(store.Paths.ProjectFile(Users.Alice, broken), "{ not a project");

        await store.MediaFiles.DeleteAsync(Users.Alice, record.Id, _none);
        Assert.Null(store.Media.Find(Users.Alice, record.Id));
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("The body must not be read.");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
