using System.Net;
using AudioLab.Server.Decoding;

namespace AudioLab.Server.Tests;

/// <summary>The real downloader, over a client whose answers the test writes: HTTPS only, pinned hosts on every hop, and the size cap.</summary>
public sealed class HttpDecoderDownloaderTests
{
    private const string Pinned = "www.gyan.dev";
    private const string Other = "evil.example";

    private static readonly string[] _hosts = [Pinned, "gyan.dev"];

    /// <summary>Plain HTTP is never sent, so no byte of the download can be read on the wire.</summary>
    [Fact]
    public async Task AnHttpSourceIsRefusedBeforeAnythingIsSent()
    {
        ScriptedHandler handler = new();
        HttpDecoderDownloader downloader = new(new HttpClient(handler));

        await Assert.ThrowsAsync<DecoderInstallFailedException>(() => Run(downloader, new Uri($"http://{Pinned}/build.zip"), 100));

        Assert.Empty(handler.Requests);
    }

    /// <summary>A host that is not pinned is refused before it is contacted.</summary>
    [Fact]
    public async Task AHostOutsideThePinIsRefusedBeforeItIsContacted()
    {
        ScriptedHandler handler = new();
        HttpDecoderDownloader downloader = new(new HttpClient(handler));

        await Assert.ThrowsAsync<DecoderInstallFailedException>(() => Run(downloader, new Uri($"https://{Other}/build.zip"), 100));

        Assert.Empty(handler.Requests);
    }

    /// <summary>A redirect to another pinned host is followed, and the body is copied.</summary>
    [Fact]
    public async Task ARedirectToAnotherPinnedHostIsFollowed()
    {
        ScriptedHandler handler = new();
        handler.Answer($"https://{Pinned}/first", HttpStatusCode.Redirect, location: "https://gyan.dev/second");
        handler.Answer("https://gyan.dev/second", HttpStatusCode.OK, [1, 2, 3]);
        HttpDecoderDownloader downloader = new(new HttpClient(handler));

        (long received, byte[] body) = await RunWithBody(downloader, new Uri($"https://{Pinned}/first"), 100);

        Assert.Equal(3, received);
        Assert.Equal(new byte[] { 1, 2, 3 }, body);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>A redirect to a host outside the pin is refused at that hop, and nothing is sent to it.</summary>
    [Fact]
    public async Task ARedirectToAHostOutsideThePinIsRefusedAtThatHop()
    {
        ScriptedHandler handler = new();
        handler.Answer($"https://{Pinned}/first", HttpStatusCode.Redirect, location: $"https://{Other}/payload");
        HttpDecoderDownloader downloader = new(new HttpClient(handler));

        await Assert.ThrowsAsync<DecoderInstallFailedException>(() => Run(downloader, new Uri($"https://{Pinned}/first"), 100));

        Assert.Single(handler.Requests);
        Assert.DoesNotContain(handler.Requests, uri => uri.Host == Other);
    }

    /// <summary>A redirect that keeps pointing at itself is stopped after the hop limit.</summary>
    [Fact]
    public async Task ARedirectLoopIsStopped()
    {
        ScriptedHandler handler = new();
        handler.Answer($"https://{Pinned}/loop", HttpStatusCode.Redirect, location: $"https://{Pinned}/loop");
        HttpDecoderDownloader downloader = new(new HttpClient(handler));

        DecoderInstallFailedException exception = await Assert.ThrowsAsync<DecoderInstallFailedException>(
            () => Run(downloader, new Uri($"https://{Pinned}/loop"), 100));

        Assert.Contains("redirected too often", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A body larger than the pinned size is stopped while it is read, so it cannot fill the disk.</summary>
    [Fact]
    public async Task AnOversizedBodyIsStoppedAtTheCap()
    {
        ScriptedHandler handler = new();
        handler.Answer($"https://{Pinned}/build.zip", HttpStatusCode.OK, new byte[1000]);
        HttpDecoderDownloader downloader = new(new HttpClient(handler));

        DecoderInstallFailedException exception = await Assert.ThrowsAsync<DecoderInstallFailedException>(
            () => Run(downloader, new Uri($"https://{Pinned}/build.zip"), 100));

        Assert.Contains("larger than the pinned size", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A server error is reported with its status.</summary>
    [Fact]
    public async Task AnErrorStatusFailsTheDownload()
    {
        ScriptedHandler handler = new();
        handler.Answer($"https://{Pinned}/build.zip", HttpStatusCode.InternalServerError, []);
        HttpDecoderDownloader downloader = new(new HttpClient(handler));

        DecoderInstallFailedException exception = await Assert.ThrowsAsync<DecoderInstallFailedException>(
            () => Run(downloader, new Uri($"https://{Pinned}/build.zip"), 100));

        Assert.Contains("HTTP status 500", exception.Message, StringComparison.Ordinal);
    }

    private static Task<long> Run(HttpDecoderDownloader downloader, Uri source, long maxBytes)
    {
        return downloader.DownloadAsync(source, _hosts, Stream.Null, maxBytes, _ => { }, CancellationToken.None);
    }

    private static async Task<(long Received, byte[] Body)> RunWithBody(HttpDecoderDownloader downloader, Uri source, long maxBytes)
    {
        using MemoryStream destination = new();
        long received = await downloader.DownloadAsync(source, _hosts, destination, maxBytes, _ => { }, CancellationToken.None);
        return (received, destination.ToArray());
    }

    /// <summary>Answers each address with the response the test set for it, and records every request it was sent.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string? Location, byte[] Body)> _answers = [];

        public List<Uri> Requests { get; } = [];

        public void Answer(string address, HttpStatusCode status, byte[]? body = null, string? location = null)
        {
            _answers[address] = (status, location, body ?? []);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            (HttpStatusCode status, string? location, byte[] body) = _answers[request.RequestUri!.AbsoluteUri];
            HttpResponseMessage response = new(status) { Content = new ByteArrayContent(body) };
            if (location is not null)
            {
                response.Headers.Location = new Uri(location);
            }
            return Task.FromResult(response);
        }
    }
}
