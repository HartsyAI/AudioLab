using System.Net;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Media;
using AudioLab.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Web.Tests;

/// <summary>Holds the media library tests: the list, uploads, refusals, cancelling and delete.</summary>
public sealed class MediaLibraryTests
{
    private static readonly Uri _site = new("http://audiolab.test/");

    private static string Summary(string id, string name) =>
        $"{{\"id\":\"{id}\",\"name\":\"{name}\",\"kind\":\"audio\",\"format\":\"wav\",\"encoding\":\"pcm16\",\"duration\":1.5,"
        + "\"sampleRate\":44100,\"channels\":2,\"bytes\":264644,"
        + "\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"created\":\"2026-11-22T10:00:00+00:00\"}";

    private static byte[] Wav(int length)
    {
        byte[] bytes = new byte[length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes((uint)(length - 8)).CopyTo(bytes, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
        return bytes;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body, string type = "application/json")
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, type) };
    }

    private static (MediaLibrary Library, ToastService Toasts) Library(HttpMessageHandler handler)
    {
        ToastService toasts = new(TimeProvider.System);
        MediaApi api = new(new HttpClient(handler) { BaseAddress = _site });
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
        bool fail = true;
        RecordingHandler handler = new(_ => fail
            ? Json(HttpStatusCode.ServiceUnavailable, "{\"title\":\"The media store is not available; try again.\"}", "application/problem+json")
            : Json(HttpStatusCode.OK, $"{{\"files\":[{Summary("m2", "b.wav")},{Summary("m1", "a.wav")}],\"refused\":1}}"));
        (MediaLibrary library, _) = Library(handler);

        await library.LoadAsync();

        Assert.Equal(LibraryState.Failed, library.State);
        Assert.Equal("The media store is not available; try again.", library.Failure);
        fail = false;
        await library.LoadAsync();
        Assert.Equal(LibraryState.Ready, library.State);
        Assert.Equal(["m2", "m1"], library.Files.Select(file => file.Id));
        Assert.Equal(1, library.Refused);
        Assert.Equal("a.wav", library.Find("m1")?.Name);
    }

    /// <summary>An uploaded file appears first in the list with the server's own measurements, and its upload row goes.</summary>
    [Fact]
    public async Task AnUploadedFileAppearsFirstAndItsRowGoes()
    {
        byte[] file = Wav(300_000);
        RecordingHandler handler = new(request => request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.Created, Summary("m9", "kick.wav"))
            : Json(HttpStatusCode.OK, $"{{\"files\":[{Summary("m1", "a.wav")}],\"refused\":0}}"));
        (MediaLibrary library, _) = Library(handler);
        await library.LoadAsync();

        library.Add([new FakeSource("kick.wav", file)]);
        await UntilAsync(() => library.Uploads.Count == 0);

        Assert.Equal(["m9", "m1"], library.Files.Select(item => item.Id));
        SentRequest sent = handler.Requests.Single(request => request.Method == HttpMethod.Post);
        Assert.Equal("/api/media?name=kick.wav", sent.Path);
        Assert.Equal(file, sent.Body);
    }

    /// <summary>A file that is not a WAV file is refused before anything is sent, with a reason, in the row and in a toast that stays.</summary>
    [Fact]
    public async Task AFileThatIsNotWavIsRefusedBeforeAnythingIsSent()
    {
        RecordingHandler handler = new(_ => Json(HttpStatusCode.Created, Summary("m9", "x.wav")));
        (MediaLibrary library, ToastService toasts) = Library(handler);

        library.Add([new FakeSource("notes.wav", Encoding.ASCII.GetBytes(new string('x', 100)))]);
        await UntilAsync(() => library.Uploads.Any(job => job.State == UploadState.Failed));

        UploadJob job = library.Uploads.Single();
        Assert.Equal("Only WAV files can be added, and this is not one.", job.Failure);
        Assert.Empty(handler.Requests);
        ToastItem toast = Assert.Single(toasts.Items);
        Assert.Equal(ToastKind.Error, toast.Kind);
        Assert.Equal("notes.wav was not added. Only WAV files can be added, and this is not one.", toast.Message);
    }

    /// <summary>A name the server would refuse is refused here, with the same words.</summary>
    [Fact]
    public async Task ANameTheServerWouldRefuseIsRefusedHere()
    {
        RecordingHandler handler = new(_ => Json(HttpStatusCode.Created, Summary("m9", "x.wav")));
        (MediaLibrary library, _) = Library(handler);

        library.Add([new FakeSource(new string('n', ProjectLimits.MaxName + 1), Wav(100))]);
        await UntilAsync(() => library.Uploads.Any(job => job.State == UploadState.Failed));

        Assert.StartsWith("The name is not valid:", library.Uploads.Single().Failure, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    /// <summary>A refusal by the server shows its words, and Try again sends the same file again.</summary>
    [Fact]
    public async Task ARefusalShowsTheServersWordsAndTryAgainSendsTheFileAgain()
    {
        int posts = 0;
        RecordingHandler handler = new(_ => ++posts == 1
            ? Json(HttpStatusCode.Conflict, "{\"title\":\"You already have 5000 media files, the most allowed; delete one first.\"}", "application/problem+json")
            : Json(HttpStatusCode.Created, Summary("m9", "kick.wav")));
        (MediaLibrary library, ToastService toasts) = Library(handler);

        library.Add([new FakeSource("kick.wav", Wav(200))]);
        await UntilAsync(() => library.Uploads.Any(job => job.State == UploadState.Failed));

        Assert.Equal("You already have 5000 media files, the most allowed; delete one first.", library.Uploads.Single().Failure);
        Assert.Contains("the most allowed", Assert.Single(toasts.Items).Message, StringComparison.Ordinal);
        library.Retry(library.Uploads.Single().Id);
        await UntilAsync(() => library.Uploads.Count == 0);
        Assert.Equal("m9", library.Files.Single().Id);
        Assert.Equal(2, posts);
    }

    /// <summary>A refused row is dismissed with cancel, and cancelling a running upload stops the request and raises no failure.</summary>
    [Fact]
    public async Task CancellingARunningUploadStopsItAndRaisesNoFailure()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GateHandler handler = new(async (_, cancellation) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellation);
            return Json(HttpStatusCode.Created, Summary("m9", "x.wav"));
        });
        (MediaLibrary library, ToastService toasts) = Library(handler);

        library.Add([new FakeSource("long.wav", Wav(500))]);
        await started.Task;
        library.Cancel(library.Uploads.Single().Id);
        await UntilAsync(() => library.Uploads.Count == 0);

        Assert.Empty(toasts.Items);
        Assert.Empty(library.Files);
    }

    /// <summary>No more than three uploads run at once, so a fourth window or tab still gets one of the server's four.</summary>
    [Fact]
    public async Task NoMoreThanThreeUploadsRunAtOnce()
    {
        int running = 0;
        int most = 0;
        int done = 0;
        GateHandler handler = new(async (_, _) =>
        {
            int now = Interlocked.Increment(ref running);
            most = Math.Max(most, now);
            await Task.Delay(30);
            Interlocked.Decrement(ref running);
            return Json(HttpStatusCode.Created, Summary($"m{Interlocked.Increment(ref done)}", "x.wav"));
        });
        (MediaLibrary library, _) = Library(handler);

        library.Add(Enumerable.Range(0, 8).Select(index => new FakeSource($"f{index}.wav", Wav(100))));
        await UntilAsync(() => library.Files.Count == 8);

        Assert.Equal(3, most);
    }

    /// <summary>A file that finished uploading before the first read of the list came back is kept by that read.</summary>
    [Fact]
    public async Task AFileThatFinishedBeforeTheFirstReadIsKept()
    {
        RecordingHandler handler = new(request => request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.Created, Summary("m9", "kick.wav"))
            : Json(HttpStatusCode.OK, $"{{\"files\":[{Summary("m1", "a.wav")}],\"refused\":0}}"));
        (MediaLibrary library, _) = Library(handler);

        library.Add([new FakeSource("kick.wav", Wav(100))]);
        await UntilAsync(() => library.Uploads.Count == 0);
        await library.LoadAsync();

        Assert.Equal(["m9", "m1"], library.Files.Select(file => file.Id));
    }

    /// <summary>A delete removes the file from the list, a file already gone counts as deleted, and a refusal keeps the file and says why.</summary>
    [Fact]
    public async Task ADeleteRemovesTheFileAndARefusalKeepsIt()
    {
        string list = $"{{\"files\":[{Summary("m3", "c.wav")},{Summary("m2", "b.wav")},{Summary("m1", "a.wav")}],\"refused\":0}}";
        RecordingHandler handler = new(request => request.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, list)
            : request.RequestUri!.AbsolutePath.EndsWith("m3", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : request.RequestUri.AbsolutePath.EndsWith("m2", StringComparison.Ordinal)
                    ? Json(HttpStatusCode.NotFound, "{\"title\":\"There is no such media file.\"}", "application/problem+json")
                    : Json(
                        HttpStatusCode.Conflict,
                        "{\"title\":\"The file is still used by your project \\\"Song\\\"; remove it from your projects before deleting it.\"}",
                        "application/problem+json"));
        (MediaLibrary library, _) = Library(handler);
        await library.LoadAsync();

        await library.DeleteAsync(library.Find("m3")!);
        await library.DeleteAsync(library.Find("m2")!);
        ApiException refused = await Assert.ThrowsAsync<ApiException>(() => library.DeleteAsync(library.Find("m1")!));

        Assert.Equal(["m1"], library.Files.Select(file => file.Id));
        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Contains("still used by your project \"Song\"", refused.Message, StringComparison.Ordinal);
    }

    private sealed class FakeSource(string name, byte[] bytes) : IUploadSource
    {
        public string Name => name;

        public long Length => bytes.Length;

        public Stream Open() => new MemoryStream(bytes);
    }

    private sealed class GateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            return await answer(request, cancellationToken);
        }
    }
}
