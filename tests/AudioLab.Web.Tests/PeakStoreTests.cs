using System.Net;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;
using AudioLab.Web.Waveforms;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of how peaks are fetched, kept and given up on.</summary>
public sealed class PeakStoreTests
{
    private static readonly Uri _site = new("http://audiolab.test/");

    private static byte[] PeakBytes(int frames, int rate = 44100)
    {
        float[] samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            samples[frame] = (frame % 100) / 200f;
        }
        PeakBuilder builder = new(1, rate);
        builder.AddFrames(samples);
        return PeakFile.Write(builder.Build());
    }

    private static HttpResponseMessage Slice(HttpRequestMessage request, byte[] file)
    {
        System.Net.Http.Headers.RangeItemHeaderValue range = request.Headers.Range!.Ranges.Single();
        int from = (int)range.From!.Value;
        int to = (int)Math.Min(range.To!.Value, file.Length - 1);
        HttpResponseMessage response = new(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(file[from..(to + 1)]) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(PeakFile.MediaType);
        return response;
    }

    private static HttpResponseMessage Refusal(HttpStatusCode status, string title, string? retryAfter = null)
    {
        HttpResponseMessage response = new(status)
        {
            Content = new StringContent($"{{\"title\":\"{title}\"}}", System.Text.Encoding.UTF8, "application/problem+json"),
        };
        if (retryAfter is not null)
        {
            response.Headers.Add("Retry-After", retryAfter);
        }
        return response;
    }

    private static PeakStore Store(HttpMessageHandler handler, TimeProvider? clock = null)
    {
        return new PeakStore(new MediaApi(new HttpClient(handler) { BaseAddress = _site }), clock ?? TimeProvider.System);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 400 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
        Assert.True(condition(), "The condition did not become true in time.");
    }

    /// <summary>The header comes first, then only the level the zoom needs, each asked for as its own range and once.</summary>
    [Fact]
    public async Task TheHeaderComesFirstThenOnlyTheLevelTheZoomNeedsEachAskedForOnce()
    {
        byte[] file = PeakBytes(200_000);
        RecordingHandler handler = new(request => Slice(request, file));
        PeakStore store = Store(handler);

        PeakView first = store.Get("m1", 34);
        store.Get("m1", 34);
        await UntilAsync(() => handler.Requests.Count == 2);
        PeakView second = await ReadyAsync(store, "m1", 34);
        store.Get("m1", 34);

        Assert.Equal(WavePhase.Loading, first.Phase);
        Assert.Equal(WavePhase.Ready, second.Phase);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(1024, second.Level!.FramesPerPeak);
        Assert.Equal(44100, second.Header!.SampleRate);
        PeakHeader header = PeakFile.ReadHeader(file);
        (long offset, long length) = PeakFile.LevelRange(header, 2);
        Assert.Equal("/api/media/m1/peaks", handler.Requests[0].Path);
        Assert.Equal($"bytes={offset}-{offset + length - 1}", RangeOf(handler, 1));
    }

    private static string RangeOf(RecordingHandler handler, int index) => handler.Requests[index].Range ?? string.Empty;

    private static async Task<PeakView> ReadyAsync(PeakStore store, string media, double pixelsPerSecond)
    {
        PeakView view = store.Get(media, pixelsPerSecond);
        for (int attempt = 0; attempt < 400 && view.Phase == WavePhase.Loading; attempt++)
        {
            await Task.Delay(5);
            view = store.Get(media, pixelsPerSecond);
        }
        return view;
    }

    /// <summary>A zoom that needs another level asks for that level only, and the first stays held.</summary>
    [Fact]
    public async Task AZoomThatNeedsAnotherLevelAsksForThatLevelOnly()
    {
        byte[] file = PeakBytes(200_000);
        RecordingHandler handler = new(request => Slice(request, file));
        PeakStore store = Store(handler);

        PeakView near = await ReadyAsync(store, "m1", 34);
        PeakView far = await ReadyAsync(store, "m1", 2);

        Assert.Equal(1024, near.Level!.FramesPerPeak);
        Assert.Equal(16384, far.Level!.FramesPerPeak);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(WavePhase.Ready, store.Get("m1", 34).Phase);
        Assert.Equal(3, handler.Requests.Count);
    }

    /// <summary>A 503 is asked again after the time the server named, a bounded number of times, and then the file has failed with the reason.</summary>
    [Fact]
    public async Task A503IsAskedAgainAfterTheTimeTheServerNamedAndThenGivenUp()
    {
        ManualClock clock = new();
        RecordingHandler handler = new(_ => Refusal(HttpStatusCode.ServiceUnavailable, "The peaks are being rebuilt; ask again in a moment.", "3"));
        PeakStore store = Store(handler, clock);

        store.Get("m1", 34);
        for (int round = 1; round < PeakStore.Attempts; round++)
        {
            await UntilAsync(() => handler.Requests.Count == round);
            Assert.Equal(WavePhase.Loading, store.Get("m1", 34).Phase);
            clock.Advance(TimeSpan.FromSeconds(3));
        }
        await UntilAsync(() => store.Get("m1", 34).Phase == WavePhase.Failed);

        Assert.Equal(PeakStore.Attempts, handler.Requests.Count);
        Assert.Contains("The peaks are being rebuilt", store.Get("m1", 34).Failure, StringComparison.Ordinal);
    }

    /// <summary>A file that is not there, or whose peaks cannot be read, fails at once without another ask, until it is retried.</summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AFileThatIsNotThereFailsAtOnceUntilItIsRetried(HttpStatusCode status)
    {
        bool fail = true;
        byte[] file = PeakBytes(50_000);
        RecordingHandler handler = new(request => fail ? Refusal(status, "There is no such media file.") : Slice(request, file));
        PeakStore store = Store(handler);
        List<string> arrived = [];
        store.Arrived += arrived.Add;

        store.Get("m1", 34);
        await UntilAsync(() => store.Get("m1", 34).Phase == WavePhase.Failed);
        store.Get("m1", 34);

        Assert.Single(handler.Requests);
        Assert.Equal("There is no such media file.", store.Get("m1", 34).Failure);
        Assert.Equal(["m1"], arrived);
        fail = false;
        store.Retry("m1");
        PeakView after = await ReadyAsync(store, "m1", 34);
        Assert.Equal(WavePhase.Ready, after.Phase);
    }

    /// <summary>A request that times out fails the file with a sentence instead of leaving it loading.</summary>
    [Fact]
    public async Task ARequestThatTimesOutFailsTheFileInsteadOfLeavingItLoading()
    {
        RecordingHandler handler = new(_ => throw new TaskCanceledException("The request timed out."));
        PeakStore store = Store(handler);
        List<string> arrived = [];
        store.Arrived += arrived.Add;

        store.Get("m1", 34);
        await UntilAsync(() => store.Get("m1", 34).Phase == WavePhase.Failed);

        Assert.Equal("The server took too long to answer. Try again.", store.Get("m1", 34).Failure);
        Assert.Equal(["m1"], arrived);
    }

    /// <summary>Peaks the server sends that are not a peak file fail the file with a sentence, not a parser error.</summary>
    [Fact]
    public async Task PeaksThatAreNotAPeakFileFailTheFile()
    {
        RecordingHandler handler = RecordingHandler.Always(HttpStatusCode.PartialContent, [1, 2, 3, 4], PeakFile.MediaType);
        PeakStore store = Store(handler);

        store.Get("m1", 34);
        await UntilAsync(() => store.Get("m1", 34).Phase == WavePhase.Failed);

        Assert.Equal("The server sent peaks this page cannot read.", store.Get("m1", 34).Failure);
    }

    /// <summary>A server that ignores the range and sends the whole file still gives the wanted bytes.</summary>
    [Fact]
    public async Task AServerThatIgnoresTheRangeStillGivesTheWantedBytes()
    {
        byte[] file = PeakBytes(100_000);
        RecordingHandler handler = RecordingHandler.Always(HttpStatusCode.OK, file, PeakFile.MediaType);
        PeakStore store = Store(handler);

        PeakView view = await ReadyAsync(store, "m1", 34);

        Assert.Equal(WavePhase.Ready, view.Phase);
        PeakData whole = PeakFile.Read(file);
        Assert.True(whole.Levels[2].Values.SequenceEqual(view.Level!.Values));
    }

    /// <summary>The least recently drawn levels go when the budget is passed, and are fetched again when drawn again.</summary>
    [Fact]
    public async Task TheLeastRecentlyDrawnLevelsGoWhenTheBudgetIsPassed()
    {
        // A zoom of 700 pixels a second picks the finest level: 31,250 peaks of 4 bytes, 125,000 bytes, for a file of this size.
        byte[] file = PeakBytes(2_000_000);
        RecordingHandler handler = new(request => Slice(request, file));
        PeakStore store = new(new MediaApi(new HttpClient(handler) { BaseAddress = _site }), TimeProvider.System, 300_000);

        for (int index = 0; index < 5; index++)
        {
            Assert.Equal(WavePhase.Ready, (await ReadyAsync(store, $"m{index}", 700)).Phase);
        }

        Assert.Equal(250_000, store.HeldBytes);
        for (int index = 5; index < 8; index++)
        {
            Assert.Equal(WavePhase.Ready, (await ReadyAsync(store, $"m{index}", 700)).Phase);
        }
        Assert.Equal(250_000, store.HeldBytes);
        int before = handler.Requests.Count;
        Assert.Equal(WavePhase.Ready, (await ReadyAsync(store, "m7", 700)).Phase);
        Assert.Equal(before, handler.Requests.Count);
        Assert.Equal(WavePhase.Ready, (await ReadyAsync(store, "m0", 700)).Phase);
        Assert.True(handler.Requests.Count > before);
    }
}
