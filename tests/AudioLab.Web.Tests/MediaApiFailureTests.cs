using System.Net;
using System.Text;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of how the media client ends: cancelling, timing out, being too large, and refusals the server words poorly.</summary>
public sealed class MediaApiFailureTests
{
    private const string ForbiddenText = "The server did not accept this page's request. Sign in again, then try again.";

    private const string Problem = "application/problem+json";

    private static readonly Uri _site = new("http://audiolab.test/");

    private static MediaApi Api(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        HttpClient normal = new(handler, false) { BaseAddress = _site };
        HttpClient uploads = new(handler, false) { BaseAddress = _site, Timeout = timeout ?? Timeout.InfiniteTimeSpan };
        return new MediaApi(normal, uploads);
    }

    private static string Summary(string extra = "", string name = "kick.wav")
    {
        return $"{{\"id\":\"m1\",\"name\":\"{name}\",\"kind\":\"audio\",\"format\":\"wav\",\"encoding\":\"pcm16\",\"duration\":1.5,"
            + "\"sampleRate\":44100,\"channels\":2,\"bytes\":264644,\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\","
            + $"\"created\":\"2026-11-22T10:00:00+00:00\"{extra}}}";
    }

    /// <summary>A stream that never produces a byte until it is cancelled, like a file that the browser is still reading.</summary>
    private sealed class StallingStream : Stream
    {
        public bool Disposed { get; private set; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>Cancelling while the file is still being read ends the upload with the cancellation, which is the user's and no failure.</summary>
    [Fact]
    public async Task CancellingWhileTheFileIsBeingReadEndsTheUploadWithTheCancellation()
    {
        using CancellationTokenSource cancel = new();
        StallingStream file = new();
        RecordingHandler handler = new(_ => throw new InvalidOperationException("The request must not get this far."));

        Task<UploadResult> upload = Run(handler, file, cancel.Token);
        await file.Started.Task;
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);
        Assert.True(file.Disposed);
    }

    private static async Task<UploadResult> Run(HttpMessageHandler handler, Stream file, CancellationToken cancellation)
    {
        await Api(handler).UploadAsync("a.wav", file, 1000, null, cancellation);
        return new UploadResult();
    }

    private sealed record UploadResult;

    /// <summary>An upload that the browser or the client ends on its own is an interruption to report, never a bare cancellation.</summary>
    [Fact]
    public async Task AnUploadThatEndsOnItsOwnIsAnInterruptionToReport()
    {
        GateHandler stall = new(async (_, cancellation) =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        UploadInterruptedException failure = await Assert.ThrowsAsync<UploadInterruptedException>(
            () => Api(stall, TimeSpan.FromMilliseconds(50)).UploadAsync("a.wav", new MemoryStream([1, 2, 3]), 3));

        Assert.Equal(UploadInterruptedException.GaveUp, failure.Message);
        Assert.Equal(failure.Message, FailureText.Of(failure));
    }

    /// <summary>A route that the ordinary timeout ends is a timeout to report, and only the caller own cancellation stays a cancellation.</summary>
    [Fact]
    public async Task ARouteThatTheTimeoutEndsIsATimeoutToReport()
    {
        GateHandler stall = new(async (_, cancellation) =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        HttpClient slow = new(stall, false) { BaseAddress = _site, Timeout = TimeSpan.FromMilliseconds(50) };
        MediaApi api = new(slow, new HttpClient(stall, false));
        using CancellationTokenSource cancel = new();

        TimeoutException timeout = await Assert.ThrowsAsync<TimeoutException>(() => api.ListAsync());
        Task listing = api.ListAsync(cancel.Token);
        await cancel.CancelAsync();

        Assert.Equal("The server took too long to answer. Try again.", FailureText.Of(timeout));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listing);
    }

    /// <summary>A file over the client limit is refused before a byte is read, and says both sizes; the limit can be replaced.</summary>
    [Fact]
    public async Task AFileOverTheClientLimitIsRefusedBeforeAByteIsRead()
    {
        StallingStream file = new();
        RecordingHandler handler = RecordingHandler.Always(HttpStatusCode.Created, Encoding.UTF8.GetBytes(Summary()), "application/json");
        MediaApi api = Api(handler);

        ApiException refused = await Assert.ThrowsAsync<ApiException>(() => api.UploadAsync("a.wav", file, MediaApi.DefaultMaxUploadBytes + 1));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, refused.Status);
        Assert.Equal("The file is too large: it is 512 MiB and the server accepts at most 512 MiB.", refused.Message);
        Assert.True(file.Disposed);
        Assert.False(file.Started.Task.IsCompleted);
        Assert.Empty(handler.Requests);
        api.MaxUploadBytes = 2048;
        ApiException lower = await Assert.ThrowsAsync<ApiException>(() => api.UploadAsync("a.wav", new MemoryStream(new byte[5000]), 5000));
        Assert.Equal("The file is too large: it is 4.9 KiB and the server accepts at most 2 KiB.", lower.Message);
        Assert.Equal(512L * 1024 * 1024, MediaApi.DefaultMaxUploadBytes);
    }

    /// <summary>A summary with a member the page does not know, or without one it needs, is refused as an unreadable answer.</summary>
    [Theory]
    [InlineData(",\"owner\":\"local-user\"")]
    [InlineData(",\"extra\":1")]
    public async Task ASummaryWithAnUnknownMemberIsRefused(string extra)
    {
        StubHandler handler = new(HttpStatusCode.OK, Summary(extra));

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(handler).GetAsync("m1"));

        Assert.Equal("The server answered with something this page cannot read.", failure.Message);
    }

    /// <summary>A summary that lacks a member, has a null one or has a wrong kind of one is refused too.</summary>
    [Theory]
    [InlineData("missing", "")]
    [InlineData("null id", "\"id\":null")]
    [InlineData("unknown kind", "\"kind\":\"video\"")]
    [InlineData("string duration", "\"duration\":\"1\"")]
    public async Task ASummaryThatLacksAMemberOrHasAWrongOneIsRefused(string what, string change)
    {
        string body = what == "missing" ? Summary().Replace("\"bytes\":264644,", string.Empty, StringComparison.Ordinal) : Summary();
        if (change.Length > 0)
        {
            string name = change[..change.IndexOf(':', StringComparison.Ordinal)];
            int from = body.IndexOf(name, StringComparison.Ordinal);
            int to = body.IndexOf(',', from);
            body = string.Concat(body.AsSpan(0, from), change, body.AsSpan(to));
        }
        StubHandler handler = new(HttpStatusCode.OK, body);

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(handler).GetAsync("m1"));

        Assert.StartsWith("The server answered with", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A listing with an unknown member is refused as well.</summary>
    [Fact]
    public async Task AListingWithAnUnknownMemberIsRefused()
    {
        StubHandler handler = new(HttpStatusCode.OK, $"{{\"files\":[{Summary(",\"owner\":\"x\"")}],\"refused\":0}}");

        await Assert.ThrowsAsync<ApiException>(() => Api(handler).ListAsync());
    }

    /// <summary>Not signed in and not allowed are sentences with what to do, whether the server says why or says nothing.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "", "text/plain", "You are not signed in, or your sign-in has ended. Sign in again, then try again.")]
    [InlineData(HttpStatusCode.Forbidden, "", "text/plain", ForbiddenText)]
    [InlineData(HttpStatusCode.Forbidden, "<html>no</html>", "text/html", ForbiddenText)]
    [InlineData(HttpStatusCode.Unauthorized, "{\"title\":\"The token expired.\"}", Problem, "The token expired. Sign in again, then try again.")]
    [InlineData(HttpStatusCode.Forbidden, "{\"title\":\"Not yours.\"}", Problem, "Not yours. Sign in again, then try again.")]
    public async Task NotSignedInAndNotAllowedAreSentencesWithWhatToDo(HttpStatusCode status, string body, string type, string expected)
    {
        StubHandler handler = new(status, body, type);

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(handler).ListAsync());

        Assert.Equal(expected, failure.Message);
    }

    /// <summary>The words of a server that sends far too many are cut so they cannot fill the screen.</summary>
    [Fact]
    public async Task AVeryLongTitleIsCut()
    {
        string title = new('x', 5000);
        StubHandler handler = new(HttpStatusCode.Conflict, $"{{\"title\":\"{title}\",\"detail\":\"{title}\"}}", "application/problem+json");

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(handler).DeleteAsync("m1"));

        Assert.True(failure.Message.Length <= (2 * ApiProblem.MaxShown) + 1);
        Assert.Contains("…", failure.Message, StringComparison.Ordinal);
    }

    private sealed class GateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return answer(request, cancellationToken);
        }
    }
}
