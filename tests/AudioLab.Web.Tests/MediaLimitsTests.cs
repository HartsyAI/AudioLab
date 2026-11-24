using System.Net;
using System.Text;
using AudioLab.Web.Components;
using AudioLab.Web.Media;
using AudioLab.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of what the media library does with the user's limits: when it reads them, what it refuses, and without them.</summary>
public sealed class MediaLimitsTests
{
    private const long MiB = 1024 * 1024;

    private static readonly Uri _site = new("http://audiolab.test/");

    private static byte[] Wav(int length)
    {
        byte[] bytes = new byte[length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes((uint)(length - 8)).CopyTo(bytes, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
        return bytes;
    }

    private static MediaLibrary Library(FakeMediaServer server, TimeProvider? clock = null)
    {
        MediaApi api = new(new HttpClient(server, false) { BaseAddress = _site }, new HttpClient(server, false) { BaseAddress = _site });
        return new MediaLibrary(api, new ToastService(TimeProvider.System), clock ?? TimeProvider.System, NullLogger<MediaLibrary>.Instance);
    }

    private static Task EndedAsync(IEnumerable<UploadJob> jobs) => Task.WhenAll(jobs.Select(job => job.Ended));

    /// <summary>The limits are read as the tab opens, and again after an upload and after a delete.</summary>
    [Fact]
    public async Task TheLimitsAreReadAtOpeningAndAfterAnUploadAndAfterADelete()
    {
        FakeMediaServer server = new(["m1"]);
        MediaLibrary library = Library(server);

        await library.LoadAsync();

        Assert.Equal(1, server.LimitReads);
        Assert.Equal(4999, library.Limits!.FilesRemaining);
        int afterOpening = server.LimitReads;
        await EndedAsync(library.Add([new FakeSource("kick.wav", Wav(300))]));
        await library.RefreshAsync();
        Assert.True(server.LimitReads > afterOpening);
        Assert.Equal(2, library.Limits!.Files);
        int afterUpload = server.LimitReads;
        await library.DeleteAsync(library.Files[0]);
        await library.RefreshAsync();
        Assert.True(server.LimitReads > afterUpload);
        Assert.Equal(1, library.Limits!.Files);
    }

    /// <summary>A file over the server's own limit is refused unread with both sizes.</summary>
    [Fact]
    public async Task AFileOverTheServersOwnLimitIsRefusedUnread()
    {
        FakeMediaServer server = new([]) { MaxUploadBytes = MiB };
        MediaLibrary library = Library(server);

        UploadJob job = library.Add([new BigSource("huge.wav", 2 * MiB)]).Single();
        await job.Ended;

        Assert.Equal("The file is too large: it is 2 MiB and the server accepts at most 1 MiB.", job.Failure);
        Assert.Empty(server.Uploads);
    }

    /// <summary>A file the server would take but the page cannot hold is refused with the page's own ceiling named as the reason.</summary>
    [Fact]
    public async Task AFileOverThePagesOwnCeilingIsRefusedAndTheCeilingIsNamed()
    {
        FakeMediaServer server = new([]) { MaxUploadBytes = 2048 * MiB };
        MediaLibrary library = Library(server);

        UploadJob job = library.Add([new BigSource("huge.wav", 600 * MiB)]).Single();
        await job.Ended;

        Assert.Equal(
            "The file is too large for this page: it is 600 MiB and the page can send at most 512 MiB (the server takes up to 2 GiB).",
            job.Failure);
        Assert.Empty(server.Uploads);
    }

    /// <summary>A file exactly as large as the bytes left, and a file that takes the last place, fit.</summary>
    [Fact]
    public async Task AFileThatTakesTheLastBytesAndTheLastPlaceFits()
    {
        FakeMediaServer bytes = new([]) { MaxBytes = 300 };
        UploadJob exact = Library(bytes).Add([new FakeSource("exact.wav", Wav(300))]).Single();
        FakeMediaServer places = new([]) { MaxFiles = 1 };
        UploadJob last = Library(places).Add([new FakeSource("last.wav", Wav(200))]).Single();

        await Task.WhenAll(exact.Ended, last.Ended);

        Assert.Single(bytes.Uploads);
        Assert.Single(places.Uploads);
        Assert.Equal(1, bytes.FileCount);
    }

    /// <summary>A full quota is said before the file is read, in the singular when one file is all that is allowed.</summary>
    [Fact]
    public async Task AFullQuotaIsSaidBeforeTheFileIsRead()
    {
        FakeMediaServer one = new(["m1"]) { MaxFiles = 1 };
        FakeMediaServer many = new(["m1", "m2"]) { MaxFiles = 2 };

        UploadJob alone = Library(one).Add([new BigSource("two.wav", 100)]).Single();
        UploadJob crowd = Library(many).Add([new BigSource("three.wav", 100)]).Single();
        await Task.WhenAll(alone.Ended, crowd.Ended);

        Assert.Equal("You already have 1 media file, the most allowed; delete it first.", alone.Failure);
        Assert.Equal("You already have 2 media files, the most allowed; delete one first.", crowd.Failure);
        Assert.Empty(one.Uploads);
    }

    /// <summary>A file past the bytes left is refused unread with the room that is left, and with exact sizes when the rounded ones agree.</summary>
    [Fact]
    public async Task AFileThatWouldTakeTheUserPastTheirBytesSaysHowMuchRoomIsLeft()
    {
        FakeMediaServer server = new([]) { MaxBytes = 10 * MiB };
        FakeMediaServer close = new([]) { MaxBytes = 10 * MiB };
        MediaLibrary library = Library(server);

        UploadJob big = library.Add([new BigSource("big.wav", 11 * MiB)]).Single();
        UploadJob alike = Library(close).Add([new BigSource("alike.wav", (10 * MiB) + 1)]).Single();
        await Task.WhenAll(big.Ended, alike.Ended);

        Assert.Equal("There is room for 10 MiB more of media and this file is 11 MiB; delete some files first.", big.Failure);
        Assert.Equal("There is room for 10,485,760 bytes more of media and this file is 10,485,761 bytes; delete some files first.", alike.Failure);
    }

    /// <summary>Files dropped together are counted against what is left together: the ones that fit go, the rest are refused unread.</summary>
    [Fact]
    public async Task FilesDroppedTogetherAreSummedAgainstTheQuota()
    {
        FakeMediaServer bytes = new([]) { MaxBytes = 700 };
        FakeMediaServer places = new([]) { MaxFiles = 1 };

        IReadOnlyList<UploadJob> byBytes = Library(bytes).Add(Enumerable.Range(0, 3).Select(index => new FakeSource($"f{index}.wav", Wav(300))));
        IReadOnlyList<UploadJob> byPlaces = Library(places).Add(Enumerable.Range(0, 3).Select(index => new FakeSource($"f{index}.wav", Wav(100))));
        await EndedAsync([.. byBytes, .. byPlaces]);

        Assert.Equal(2, bytes.Uploads.Count);
        Assert.Single(places.Uploads);
        Assert.Equal(1, byBytes.Count(job => job.State == UploadState.Failed));
        Assert.Equal(2, byPlaces.Count(job => job.State == UploadState.Failed));
        UploadJob refused = byBytes.Single(job => job.State == UploadState.Failed);
        Assert.StartsWith("There is room for 100 B more of media", refused.Failure, StringComparison.Ordinal);
    }

    /// <summary>A delete that ends while a refresh is on its way is in the limits that the next file is judged on.</summary>
    [Fact]
    public async Task ADeleteThatEndsDuringARefreshIsInTheLimits()
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeMediaServer server = new([]) { MaxBytes = 2100 };
        server.Add("m1", "a.wav", 1000);
        server.Add("m2", "b.wav", 1000);
        MediaLibrary library = Library(server);
        await library.LoadAsync();
        Assert.Equal(100, library.Limits!.BytesRemaining);
        server.BeforeLimitsAnswer = read => read == 2 ? gate.Task : Task.CompletedTask;

        await library.DeleteAsync(library.Find("m1")!);
        await library.DeleteAsync(library.Find("m2")!);
        gate.SetResult();
        await library.RefreshAsync();
        Assert.Equal(2100, library.Limits!.BytesRemaining);
        UploadJob job = library.Add([new FakeSource("big.wav", Wav(1500))]).Single();
        await job.Ended;

        Assert.Null(job.Failure);
        Assert.Single(server.Uploads);
    }

    /// <summary>Try again on a quota refusal reads the limits afresh, so a quota that has room by then lets the file go.</summary>
    [Fact]
    public async Task TryAgainReadsTheLimitsAfresh()
    {
        FakeMediaServer server = new(["m1"]) { MaxFiles = 1 };
        MediaLibrary library = Library(server);
        UploadJob job = library.Add([new FakeSource("kick.wav", Wav(200))]).Single();
        await job.Ended;
        Assert.Equal(UploadState.Failed, job.State);
        Assert.Empty(server.Uploads);

        server.MaxFiles = 2;
        library.Retry(job.Id);
        await job.Ended;

        Assert.Null(job.Failure);
        Assert.Single(server.Uploads);
    }

    /// <summary>A read that fails after a delete says nothing about the quota: the old numbers are dropped and the server judges the file.</summary>
    [Fact]
    public async Task AFailedReadAfterADeleteDropsTheOldNumbers()
    {
        FakeMediaServer server = new(["m1"]) { MaxFiles = 1 };
        MediaLibrary library = Library(server);
        await library.LoadAsync();
        Assert.Equal(0, library.Limits!.FilesRemaining);

        server.LimitsStatus = HttpStatusCode.InternalServerError;
        await library.DeleteAsync(library.Files[0]);
        await library.RefreshAsync();
        UploadJob job = library.Add([new FakeSource("kick.wav", Wav(200))]).Single();
        await job.Ended;

        Assert.Null(library.Limits);
        Assert.Null(job.Failure);
        Assert.Single(server.Uploads);
    }

    /// <summary>When the server cannot say what the limits are the file is sent and the server judges it, and says no when it must.</summary>
    [Fact]
    public async Task WhenTheLimitsCannotBeReadTheServerJudgesTheFile()
    {
        FakeMediaServer server = new([]) { LimitsStatus = HttpStatusCode.NotFound, MaxFiles = 1 };
        server.Add("m1", "a.wav");
        MediaLibrary library = Library(server);

        UploadJob job = library.Add([new FakeSource("kick.wav", Wav(200))]).Single();
        await job.Ended;

        Assert.Null(library.Limits);
        Assert.Equal(UploadState.Failed, job.State);
        Assert.Contains("the most allowed", job.Failure, StringComparison.Ordinal);
    }

    /// <summary>A limits route that times out is the same: the page does not wait on it and the server judges the file.</summary>
    [Fact]
    public async Task ALimitsRouteThatTimesOutLeavesTheJudgingToTheServer()
    {
        FakeMediaServer server = new([]) { LimitsTimeOut = true };
        MediaLibrary library = Library(server);

        UploadJob job = library.Add([new FakeSource("kick.wav", Wav(200))]).Single();
        await job.Ended;

        Assert.Null(library.Limits);
        Assert.Null(job.Failure);
        Assert.Single(server.Uploads);
    }

    /// <summary>A limits route that never answers holds the files for a few seconds, not for the client's whole timeout, and only once.</summary>
    [Fact]
    public async Task ALimitsRouteThatNeverAnswersHoldsTheFilesOnlyForTheWait()
    {
        ManualClock clock = new();
        FakeMediaServer server = new([]) { BeforeLimitsAnswer = _ => new TaskCompletionSource().Task };
        MediaLibrary library = Library(server, clock);

        IReadOnlyList<UploadJob> jobs = library.Add([new FakeSource("a.wav", Wav(200)), new FakeSource("b.wav", Wav(200))]);
        Assert.Empty(server.Uploads);
        clock.Advance(UploadLimits.Patience);
        await EndedAsync(jobs);

        Assert.Equal(2, server.Uploads.Count);
        Assert.All(jobs, job => Assert.Null(job.Failure));
        UploadJob later = library.Add([new FakeSource("c.wav", Wav(200))]).Single();
        await later.Ended;
        Assert.Equal(3, server.Uploads.Count);
    }

    /// <summary>The places for uploads follow the server's number of uploads a user may run, keeping one free for another tab.</summary>
    [Fact]
    public async Task ThePlacesForUploadsFollowTheServersNumber()
    {
        FakeMediaServer two = new([]) { MaxConcurrentUploads = 2, UploadDelay = TimeSpan.FromMilliseconds(15) };
        FakeMediaServer four = new([]) { UploadDelay = TimeSpan.FromMilliseconds(15) };

        await EndedAsync(Library(two).Add(Enumerable.Range(0, 5).Select(index => new FakeSource($"f{index}.wav", Wav(100)))));
        await EndedAsync(Library(four).Add(Enumerable.Range(0, 5).Select(index => new FakeSource($"f{index}.wav", Wav(100)))));

        Assert.Equal(1, two.MostAtOnce);
        Assert.Equal(3, four.MostAtOnce);
        Assert.Equal(5, two.Uploads.Count);
    }

    /// <summary>A body that is not the limits the client knows is refused like any other answer, not read leniently.</summary>
    [Fact]
    public async Task ALimitsAnswerWithAnUnknownMemberIsAnApiFailure()
    {
        StubHandler handler = new(HttpStatusCode.OK, "{\"owner\":\"u1\",\"maxUploadBytes\":1}");
        MediaApi api = new(new HttpClient(handler, false) { BaseAddress = _site }, new HttpClient(handler, false) { BaseAddress = _site });

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => api.LimitsAsync());

        Assert.Equal(HttpStatusCode.OK, failure.Status);
        Assert.Equal("The server answered with something this page cannot read.", failure.Message);
    }

    private sealed class FakeSource(string name, byte[] bytes) : IUploadSource
    {
        public string Name => name;

        public long Length => bytes.Length;

        public Stream Open() => new MemoryStream(bytes);
    }

    private sealed class BigSource(string name, long length) : IUploadSource
    {
        public string Name => name;

        public long Length => length;

        public Stream Open() => throw new InvalidOperationException("A file that is refused by the limits must not be opened.");
    }
}
