using System.Net;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Media;
using AudioLab.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Web.Tests;

/// <summary>Holds the media library tests: the list, uploads, refusals, cancelling, timeouts and delete.</summary>
public sealed class MediaLibraryTests
{
    private static readonly Uri _site = new("http://audiolab.test/");

    private static byte[] Wav(int length)
    {
        byte[] bytes = new byte[length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes((uint)(length - 8)).CopyTo(bytes, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
        return bytes;
    }

    private static (MediaLibrary Library, ToastService Toasts) Library(FakeMediaServer server)
    {
        ToastService toasts = new(TimeProvider.System);
        MediaApi api = new(new HttpClient(server, false) { BaseAddress = _site }, new HttpClient(server, false) { BaseAddress = _site });
        return (new MediaLibrary(api, toasts, TimeProvider.System, NullLogger<MediaLibrary>.Instance), toasts);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
        Assert.True(condition(), "The condition did not become true in time.");
    }

    /// <summary>The list is read once, newest first, and a read that fails says why and can be repeated.</summary>
    [Fact]
    public async Task TheListIsReadAndAFailedReadCanBeRepeated()
    {
        FakeMediaServer server = new(["m1", "m2"]) { ListStatus = HttpStatusCode.ServiceUnavailable };
        (MediaLibrary library, _) = Library(server);

        await library.LoadAsync();

        Assert.Equal(LibraryState.Failed, library.State);
        Assert.Equal("The media store is not available; try again.", library.Failure);
        server.ListStatus = HttpStatusCode.OK;
        await library.LoadAsync();
        Assert.Equal(LibraryState.Ready, library.State);
        Assert.Equal(["m2", "m1"], library.Files.Select(file => file.Id));
        Assert.Equal("a.wav", library.Find("m1")?.Name);
    }

    /// <summary>A list read that times out is a failure with a reason and Try again, not a list that stays unloaded.</summary>
    [Fact]
    public async Task AListReadThatTimesOutIsAFailureWithAReason()
    {
        FakeMediaServer server = new([]) { TimeOut = HttpMethod.Get };
        (MediaLibrary library, _) = Library(server);

        await library.LoadAsync();

        Assert.Equal(LibraryState.Failed, library.State);
        Assert.Equal("The server took too long to answer. Try again.", library.Failure);
        server.TimeOut = null;
        await library.LoadAsync();
        Assert.Equal(LibraryState.Ready, library.State);
    }

    /// <summary>An uploaded file appears first, its row goes, and the list is read again so what the server holds is what is shown.</summary>
    [Fact]
    public async Task AnUploadedFileAppearsFirstItsRowGoesAndTheListIsReadAgain()
    {
        FakeMediaServer server = new(["m1"]);
        (MediaLibrary library, ToastService toasts) = Library(server);
        await library.LoadAsync();
        int reads = server.Reads;

        library.Add([new FakeSource("kick.wav", Wav(300_000))]);
        await UntilAsync(() => library.Uploads.Count == 0 && server.Reads > reads);

        Assert.Equal(["m2", "m1"], library.Files.Select(item => item.Id));
        Assert.Equal(("/api/media?name=kick.wav", 300_000), (server.Uploads.Single().Path, server.Uploads.Single().Body.Length));
        Assert.Equal("kick.wav was added.", Assert.Single(toasts.Items).Message);
    }

    /// <summary>A file that is not a WAV file is refused before anything is sent, with a reason, in the row and in one toast that stays.</summary>
    [Fact]
    public async Task AFileThatIsNotWavIsRefusedBeforeAnythingIsSent()
    {
        FakeMediaServer server = new([]);
        (MediaLibrary library, ToastService toasts) = Library(server);

        library.Add([new FakeSource("notes.wav", Encoding.ASCII.GetBytes(new string('x', 100)))]);
        await UntilAsync(() => library.Uploads.Any(job => job.State == UploadState.Failed));

        UploadJob job = library.Uploads.Single();
        Assert.Equal("Only WAV files can be added, and this is not one.", job.Failure);
        Assert.Empty(server.Uploads);
        ToastItem toast = Assert.Single(toasts.Items);
        Assert.Equal(ToastKind.Error, toast.Kind);
        Assert.Equal("notes.wav was not added. Only WAV files can be added, and this is not one.", toast.Message);
    }

    /// <summary>A name the server would refuse is refused here, with the same words, and a file over the limit is refused unread.</summary>
    [Fact]
    public async Task ANameTheServerWouldRefuseAndAFileOverTheLimitAreRefusedHere()
    {
        FakeMediaServer server = new([]);
        (MediaLibrary library, _) = Library(server);

        string longName = new('n', ProjectLimits.MaxName + 1);
        library.Add([new FakeSource(longName, Wav(100)), new BigSource("huge.wav", MediaApi.DefaultMaxUploadBytes + 1)]);
        await UntilAsync(() => library.Uploads.Count(job => job.State == UploadState.Failed) == 2);

        Assert.StartsWith("The name is not valid:", library.Uploads[0].Failure, StringComparison.Ordinal);
        Assert.Equal("The file is too large: it is 512 MiB and the server accepts at most 512 MiB.", library.Uploads[1].Failure);
        Assert.Empty(server.Uploads);
    }

    /// <summary>A refusal by the server shows its words, and Try again sends the same file again.</summary>
    [Fact]
    public async Task ARefusalShowsTheServersWordsAndTryAgainSendsTheFileAgain()
    {
        FakeMediaServer server = new([]) { NextUploads = [HttpStatusCode.Conflict] };
        (MediaLibrary library, ToastService toasts) = Library(server);

        library.Add([new FakeSource("kick.wav", Wav(200))]);
        await UntilAsync(() => library.Uploads.Any(job => job.State == UploadState.Failed));

        Assert.Equal("You already have 5000 media files, the most allowed; delete one first.", library.Uploads.Single().Failure);
        Assert.Contains("the most allowed", Assert.Single(toasts.Items).Message, StringComparison.Ordinal);
        library.Retry(library.Uploads.Single().Id);
        await UntilAsync(() => library.Uploads.Count == 0);
        Assert.Equal("m1", library.Files.Single().Id);
        Assert.Equal(2, server.Uploads.Count);
    }

    /// <summary>Cancelling a running upload stops the request and raises no failure; cancelling a refused row dismisses it.</summary>
    [Fact]
    public async Task CancellingARunningUploadStopsItAndDismissingARefusedRowRemovesIt()
    {
        FakeMediaServer server = new([]) { Stall = true };
        (MediaLibrary library, ToastService toasts) = Library(server);
        library.Add([new FakeSource("long.wav", Wav(500))]);
        await UntilAsync(() => server.Stalled);

        library.Cancel(library.Uploads.Single().Id);
        await UntilAsync(() => library.Uploads.Count == 0);

        Assert.Empty(toasts.Items);
        Assert.Empty(library.Files);
        library.Add([new FakeSource("bad.wav", Encoding.ASCII.GetBytes(new string('x', 100)))]);
        await UntilAsync(() => library.Uploads.Any(job => job.State == UploadState.Failed));
        library.Cancel(library.Uploads.Single().Id);
        Assert.Empty(library.Uploads);
    }

    /// <summary>An upload that the browser ends on its own stays as a refused row with the reason; only the user cancel removes a row.</summary>
    [Fact]
    public async Task AnUploadThatTheBrowserEndsOnItsOwnStaysAsARefusedRow()
    {
        FakeMediaServer server = new([]) { TimeOut = HttpMethod.Post };
        (MediaLibrary library, ToastService toasts) = Library(server);

        library.Add([new FakeSource("kick.wav", Wav(500))]);
        await UntilAsync(() => library.Uploads.Any(job => job.State == UploadState.Failed));

        Assert.Equal(UploadInterruptedException.GaveUp, library.Uploads.Single().Failure);
        Assert.Contains("kick.wav was not added.", Assert.Single(toasts.Items).Message, StringComparison.Ordinal);
        server.TimeOut = null;
        library.Retry(library.Uploads.Single().Id);
        await UntilAsync(() => library.Uploads.Count == 0);
        Assert.Equal("m1", library.Files.Single().Id);
    }

    /// <summary>No more than three uploads run at once, so a fourth window or tab still gets one of the server four.</summary>
    [Fact]
    public async Task NoMoreThanThreeUploadsRunAtOnce()
    {
        FakeMediaServer server = new([]) { UploadDelay = TimeSpan.FromMilliseconds(30) };
        (MediaLibrary library, _) = Library(server);

        library.Add(Enumerable.Range(0, 8).Select(index => new FakeSource($"f{index}.wav", Wav(100))));
        await UntilAsync(() => library.Files.Count == 8);

        Assert.Equal(3, server.MostAtOnce);
    }

    /// <summary>The files and the failures of one batch are each said once: one toast of each kind, however many files there were.</summary>
    [Fact]
    public async Task TheFilesAndTheFailuresOfOneBatchAreEachSaidOnce()
    {
        FakeMediaServer server = new(["m1"]);
        (MediaLibrary library, ToastService toasts) = Library(server);
        await library.LoadAsync();

        library.Add(
        [
            new FakeSource("a.wav", Wav(100)),
            new FakeSource("b.wav", Wav(100)),
            new FakeSource("c.wav", Wav(100)),
            new FakeSource("bad1.wav", Encoding.ASCII.GetBytes(new string('x', 100))),
            new FakeSource("bad2.wav", Encoding.ASCII.GetBytes(new string('y', 100))),
        ]);
        await UntilAsync(() => toasts.Items.Count == 2);

        Assert.Equal("3 files were added. One has the name of a file you already had.", toasts.Items[0].Message);
        Assert.Equal(ToastKind.Success, toasts.Items[0].Kind);
        Assert.Equal(ToastKind.Error, toasts.Items[1].Kind);
        Assert.StartsWith("2 files were not added; they are listed in the Media tab. The first, bad1.wav:", toasts.Items[1].Message, Ordinal);
    }

    /// <summary>A file that finished uploading before the first read of the list came back is kept by that read.</summary>
    [Fact]
    public async Task AFileThatFinishedBeforeTheFirstReadIsKept()
    {
        FakeMediaServer server = new(["m1"]);
        (MediaLibrary library, _) = Library(server);

        library.Add([new FakeSource("kick.wav", Wav(100))]);
        await UntilAsync(() => library.Uploads.Count == 0);
        await library.LoadAsync();

        Assert.Equal(["m2", "m1"], library.Files.Select(file => file.Id));
    }

    /// <summary>A read that began before a delete and answers after it never brings the deleted file back.</summary>
    [Fact]
    public async Task AReadThatBeganBeforeADeleteNeverBringsTheDeletedFileBack()
    {
        FakeMediaServer server = new(["m1", "m2"]);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        server.HoldFirstList = release.Task;
        (MediaLibrary library, _) = Library(server);

        Task load = library.LoadAsync();
        await UntilAsync(() => server.Reads == 1);
        await library.DeleteAsync(library.Find("m1") ?? Gone("m1"));
        release.SetResult();
        await load;

        Assert.Equal(["m2"], library.Files.Select(file => file.Id));
    }

    /// <summary>A delete removes the file, one already gone counts as deleted, a refusal keeps it and a timeout is thrown for the dialog.</summary>
    [Fact]
    public async Task ADeleteRemovesTheFileAndARefusalOrATimeoutKeepsIt()
    {
        FakeMediaServer server = new(["m1", "m2", "m3"]) { InUse = ["m1"], Gone = ["m2"] };
        (MediaLibrary library, _) = Library(server);
        await library.LoadAsync();

        await library.DeleteAsync(library.Find("m3")!);
        await library.DeleteAsync(library.Find("m2")!);
        ApiException refused = await Assert.ThrowsAsync<ApiException>(() => library.DeleteAsync(library.Find("m1")!));
        server.TimeOut = HttpMethod.Delete;
        TimeoutException slow = await Assert.ThrowsAsync<TimeoutException>(() => library.DeleteAsync(library.Find("m1")!));

        Assert.Equal(["m1"], library.Files.Select(file => file.Id));
        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Contains("still used by your project \"Song\"", refused.Message, StringComparison.Ordinal);
        Assert.Equal("The server took too long to answer. Try again.", FailureText.Of(slow));
    }

    /// <summary>Reading the list again keeps what is shown when the read fails, and takes the server list when it answers.</summary>
    [Fact]
    public async Task ReadingTheListAgainKeepsWhatIsShownWhenTheReadFails()
    {
        FakeMediaServer server = new(["m1"]);
        (MediaLibrary library, _) = Library(server);
        await library.LoadAsync();
        server.Add("m2", "b.wav");

        server.ListStatus = HttpStatusCode.ServiceUnavailable;
        await library.RefreshAsync();
        Assert.Equal(["m1"], library.Files.Select(file => file.Id));
        server.ListStatus = HttpStatusCode.OK;
        await library.RefreshAsync();

        Assert.Equal(LibraryState.Ready, library.State);
        Assert.Equal(["m2", "m1"], library.Files.Select(file => file.Id));
    }

    private const StringComparison Ordinal = StringComparison.Ordinal;

    /// <summary>A batch of compressed files goes to the server one at a time, and every one of them is decoded and added.</summary>
    [Fact]
    public async Task ACompressedBatchGoesOneAtATimeAndEachFileIsAdded()
    {
        FakeMediaServer server = new([]) { UploadDelay = TimeSpan.FromMilliseconds(30) };
        (MediaLibrary library, _) = Library(server);

        library.Add(
        [
            new CompressedSource("a.mp3", "audio/mpeg", [1, 2, 3]),
            new CompressedSource("b.mp3", "audio/mpeg", [4, 5, 6]),
            new CompressedSource("c.flac", "audio/flac", [7, 8, 9]),
        ]);
        await UntilAsync(() => library.Files.Count == 3);

        // The server allows one decode per user, so a batch of three is sent in turn, and none of them is refused as busy.
        Assert.Equal(1, server.MostAtOnce);
        Assert.Equal(3, library.Files.Count);
    }

    /// <summary>Cancelling a compressed file that is still waiting its turn takes no place, so the next one still goes one at a time.</summary>
    [Fact]
    public async Task CancellingAWaitingCompressedFileDoesNotFreeAPlaceItNeverHad()
    {
        FakeMediaServer server = new([]) { UploadDelay = TimeSpan.FromMilliseconds(80) };
        (MediaLibrary library, _) = Library(server);

        IReadOnlyList<UploadJob> jobs = library.Add(
        [
            new CompressedSource("a.mp3", "audio/mpeg", [1, 2, 3]),
            new CompressedSource("b.mp3", "audio/mpeg", [4, 5, 6]),
            new CompressedSource("c.flac", "audio/flac", [7, 8, 9]),
        ]);
        // The first is sending, so the other two are waiting for the one decode place.
        await UntilAsync(() => server.MostAtOnce == 1);
        library.Cancel(jobs[1].Id);
        await UntilAsync(() => library.Files.Count == 2);

        Assert.Equal(1, server.MostAtOnce);
        Assert.Equal(2, library.Files.Count);
    }

    private static MediaSummary Gone(string id)
    {
        return new MediaSummary(
            id, "a.wav", MediaKind.Audio, MediaFormat.Wav, SampleEncoding.Pcm16, 1, 44100, 2, 1, new string('0', 64), DateTimeOffset.UnixEpoch);
    }

    private sealed class FakeSource(string name, byte[] bytes) : IUploadSource
    {
        public string Name => name;

        public long Length => bytes.Length;

        public Stream Open() => new MemoryStream(bytes);
    }

    private sealed class CompressedSource(string name, string contentType, byte[] bytes) : IUploadSource
    {
        public string Name => name;

        public string ContentType => contentType;

        public long Length => bytes.Length;

        public Stream Open() => new MemoryStream(bytes);
    }

    private sealed class BigSource(string name, long length) : IUploadSource
    {
        public string Name => name;

        public long Length => length;

        public Stream Open() => throw new InvalidOperationException("A file over the limit must not be opened.");
    }
}
