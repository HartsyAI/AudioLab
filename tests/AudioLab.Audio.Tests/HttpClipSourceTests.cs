using System.Net;
using AudioLab.Audio.Live;
using AudioLab.Core.Model;
using AudioLab.Web.Interop.Worker;

namespace AudioLab.Audio.Tests;

public sealed class HttpClipSourceTests
{
    private static readonly MediaId _clip = MediaId.From("med_a");

    private sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] answers) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            Func<HttpRequestMessage, HttpResponseMessage> answer = answers[Math.Min(Requests.Count, answers.Length) - 1];
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private static HttpResponseMessage Timeout(HttpRequestMessage request) => throw new TaskCanceledException("The request timed out.");

    private static HttpResponseMessage Wav(HttpRequestMessage request)
    {
        using MemoryStream stream = new();
        RenderedAudio audio = new(new AudioFormat(8000, 2), new float[800], new float[800]);
        WavExporter.Write(audio, stream, new WavOptions { Format = WavFormat.Pcm16 });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(stream.ToArray()) };
    }

    private static HttpClipSource Source(ScriptedHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://example.test/app/") }, [TimeSpan.Zero, TimeSpan.Zero]);

    private static Task<PcmAudio> LoadAsync(HttpClipSource source, CancellationToken token = default) =>
        source.LoadAsync(_clip, _ => Task.CompletedTask, token);

    [Fact]
    public async Task AClipIsFetchedFromTheContentRouteUnderThePagesBaseAddressAndDecoded()
    {
        ScriptedHandler handler = new(Wav);

        PcmAudio audio = await LoadAsync(Source(handler));

        Assert.Equal(["/app/api/media/med_a/content"], handler.Requests);
        Assert.Equal(800, audio.Frames);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "was not found")]
    [InlineData(HttpStatusCode.Unauthorized, "not signed in")]
    [InlineData(HttpStatusCode.Forbidden, "not signed in")]
    public async Task ARefusalIsSaidPlainlyAndNeverTriedAgain(HttpStatusCode status, string words)
    {
        ScriptedHandler handler = new(_ => Status(status));

        ClipUnavailableException error = await Assert.ThrowsAsync<ClipUnavailableException>(() => LoadAsync(Source(handler)));

        Assert.Contains(words, error.Message);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ABusyServerAndATimeoutAreTriedAgainAndAClipThatArrivesLaterPlays()
    {
        ScriptedHandler handler = new(_ => Status(HttpStatusCode.ServiceUnavailable), Timeout, Wav);

        PcmAudio audio = await LoadAsync(Source(handler));

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(800, audio.Frames);
    }

    [Fact]
    public async Task AFetchThatKeepsTimingOutGivesUpWithTheReasonAfterThreeTries()
    {
        ScriptedHandler handler = new(Timeout);

        ClipUnavailableException error = await Assert.ThrowsAsync<ClipUnavailableException>(() => LoadAsync(Source(handler)));

        Assert.Contains("timed out", error.Message);
        Assert.Contains("3 tries", error.Message);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task AFileOverTheLimitIsRefusedBeforeItIsRead()
    {
        ScriptedHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentLength = HttpClipSource.MaxClipBytes + 1;
            return response;
        });

        ClipUnavailableException error = await Assert.ThrowsAsync<ClipUnavailableException>(() => LoadAsync(Source(handler)));

        Assert.Contains("over the 128 MB", error.Message);
    }

    [Fact]
    public async Task ACancelledFetchIsNotTriedAgain()
    {
        using CancellationTokenSource cancel = new();
        ScriptedHandler handler = new(request =>
        {
            cancel.Cancel();
            throw new TaskCanceledException("cancelled", null, cancel.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LoadAsync(Source(handler), cancel.Token));

        Assert.Single(handler.Requests);
    }
}
