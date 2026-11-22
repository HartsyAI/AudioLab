using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Media;
using AudioLab.Server.Realtime;
using AudioLab.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

public sealed class MediaRouteTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private static Task<RunningServer> StartWith(string key, string value)
    {
        return RunningServer.StartAsync(new Dictionary<string, string?> { [key] = value });
    }

    private static Dictionary<string, string?> Swarm() => new() { ["AudioLab:Door:Kind"] = "Swarm", ["AudioLab:Door:ProxySecret"] = Secret };

    private static byte[] Wav(int frames = 24000) => TestWav.Build(SampleEncoding.Pcm16, 2, 48000, frames);

    private static HttpRequestMessage Request(HttpMethod method, string path, string? user = null, HttpContent? content = null)
    {
        HttpRequestMessage request = new(method, path) { Content = content };
        if (user is not null)
        {
            request.Headers.Add("X-AudioLab-Secret", Secret);
            request.Headers.Add("X-AudioLab-User", user);
        }
        return request;
    }

    private static HttpContent WavBody(byte[] file)
    {
        ByteArrayContent content = new(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        return content;
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        RunningServer server,
        byte[] file,
        string name = "kick.wav",
        string? user = null,
        string type = "audio/wav")
    {
        ByteArrayContent content = new(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/media?name={Uri.EscapeDataString(name)}", user, content);
        return await server.Client.SendAsync(request);
    }

    private static async Task<MediaSummary> UploadedAsync(RunningServer server, byte[] file, string name = "kick.wav", string? user = null)
    {
        using HttpResponseMessage response = await UploadAsync(server, file, name, user);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;
    }

    private static async Task<HttpResponseMessage> SendAsync(RunningServer server, HttpMethod method, string path, string? user = null)
    {
        using HttpRequestMessage request = Request(method, path, user);
        return await server.Client.SendAsync(request);
    }

    private static async Task<HttpStatusCode?> StatusOrResetAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            using HttpResponseMessage response = await send();
            return response.StatusCode;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static string[] Media(RunningServer server) => Directory.GetFiles(server.StoreFolder, "m*", SearchOption.AllDirectories);

    [Fact]
    public async Task AWavUploadedOverHttpComesBackByteForByteWithItsMeasuredRecord()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        byte[] file = Wav();

        using HttpResponseMessage created = await UploadAsync(server, file, "Snare 01.wav");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        MediaSummary summary = (await created.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;
        Assert.Equal($"/api/media/{summary.Id}", created.Headers.Location?.ToString());
        Assert.Equal("Snare 01.wav", summary.Name);
        Assert.Equal(0.5, summary.Duration);
        Assert.Equal(48000, summary.SampleRate);
        Assert.Equal(2, summary.Channels);
        Assert.Equal(file.Length, summary.Bytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(file)), summary.Sha256);
        string json = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain("owner", json, StringComparison.Ordinal);
        Assert.DoesNotContain(UserId.Local.Value, json, StringComparison.Ordinal);

        using HttpResponseMessage downloaded = await server.Client.GetAsync($"/api/media/{summary.Id}/content");
        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.Equal("audio/wav", downloaded.Content.Headers.ContentType?.MediaType);
        Assert.Equal("bytes", downloaded.Headers.AcceptRanges.Single());
        Assert.Equal($"\"{summary.Sha256}\"", downloaded.Headers.ETag?.Tag);
        Assert.Equal("nosniff", downloaded.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("immutable", downloaded.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Equal(file, await downloaded.Content.ReadAsByteArrayAsync());

        MediaSummary read = (await server.Client.GetFromJsonAsync($"/api/media/{summary.Id}", MediaJsonContext.Default.MediaSummary))!;
        Assert.Equal(summary, read);
        MediaListing listing = (await server.Client.GetFromJsonAsync("/api/media", MediaJsonContext.Default.MediaListing))!;
        Assert.Equal([summary], listing.Files);
        Assert.Equal(0, listing.Refused);

        Assert.Equal(HttpStatusCode.NoContent, (await server.Client.DeleteAsync($"/api/media/{summary.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync($"/api/media/{summary.Id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.DeleteAsync($"/api/media/{summary.Id}")).StatusCode);
        Assert.Empty(Media(server));
    }

    [Fact]
    public async Task AHeadRequestAnswersWithTheHeadersOfTheFileAndNoBody()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        byte[] file = Wav();
        MediaSummary summary = await UploadedAsync(server, file);

        using HttpRequestMessage request = new(HttpMethod.Head, $"/api/media/{summary.Id}/content");
        using HttpResponseMessage response = await server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(file.Length, response.Content.Headers.ContentLength);
        Assert.Equal($"\"{summary.Sha256}\"", response.Headers.ETag?.Tag);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ABodyThatEndsBeforeItsAnnouncedLengthLeavesNothingAndTheServerGoesOn()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        byte[] file = Wav();
        Uri address = server.Client.BaseAddress!;
        using System.Net.Sockets.TcpClient client = new();
        await client.ConnectAsync(address.Host, address.Port);
        await using System.Net.Sockets.NetworkStream stream = client.GetStream();
        string[] lines =
        [
            "POST /api/media?name=short.wav HTTP/1.1",
            $"Host: {address.Authority}",
            "Content-Type: audio/wav",
            $"Content-Length: {file.Length + 5000}",
            string.Empty,
            string.Empty,
        ];
        string head = string.Join("\r\n", lines);
        await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(file);
        client.Close();

        await Task.Delay(500);
        Assert.Empty(Media(server));
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/api/media")).StatusCode);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(408)]
    [InlineData(413)]
    public async Task AClientsOwnFailureIsAnsweredWithItsOwnStatusAndNeverAsAStoreFailure(int status)
    {
        MediaErrorFilter filter = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaErrorFilter>.Instance);
        Microsoft.AspNetCore.Http.DefaultHttpContext http = new();
        EndpointFilterInvocationContext context = new DefaultEndpointFilterInvocationContext(http);

        object? result = await filter.InvokeAsync(context, _ => throw new Microsoft.AspNetCore.Http.BadHttpRequestException("cut", status));
        object? reset = await filter.InvokeAsync(context, _ => throw new Microsoft.AspNetCore.Connections.ConnectionResetException("gone"));

        Assert.Equal(status, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)result!).StatusCode);
        Assert.Equal(400, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)reset!).StatusCode);
    }

    [Fact]
    public async Task BytesWithNoRecordAreSweptWhenTheServerStartsNotAtTheFirstRequest()
    {
        using TempFolder folder = new();
        AudioLab.Server.Store.StorePaths paths = new(folder.Path);
        string orphan = paths.MediaDataFile(UserId.Local, MediaId.From("med_orphan"));
        Directory.CreateDirectory(Path.GetDirectoryName(orphan)!);
        await File.WriteAllBytesAsync(orphan, [1, 2, 3]);

        await using RunningServer server = await StartWith("AudioLab:Storage:Root", folder.Path);

        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public async Task RangeRequestsAreAnsweredWithTheRightBytesAndAnUnsatisfiableOneWith416()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        byte[] file = Wav();
        MediaSummary summary = await UploadedAsync(server, file);
        string path = $"/api/media/{summary.Id}/content";

        async Task<HttpResponseMessage> Get(Action<HttpRequestHeaders> headers)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            headers(request.Headers);
            return await server.Client.SendAsync(request);
        }

        using HttpResponseMessage middle = await Get(headers => headers.Range = new RangeHeaderValue(100, 199));
        Assert.Equal(HttpStatusCode.PartialContent, middle.StatusCode);
        Assert.Equal(new ContentRangeHeaderValue(100, 199, file.Length), middle.Content.Headers.ContentRange);
        Assert.Equal(file[100..200], await middle.Content.ReadAsByteArrayAsync());

        using HttpResponseMessage tail = await Get(headers => headers.Range = new RangeHeaderValue(null, 50));
        Assert.Equal(HttpStatusCode.PartialContent, tail.StatusCode);
        Assert.Equal(file[^50..], await tail.Content.ReadAsByteArrayAsync());

        using HttpResponseMessage open = await Get(headers => headers.Range = new RangeHeaderValue(file.Length - 10, null));
        Assert.Equal(file[^10..], await open.Content.ReadAsByteArrayAsync());

        using HttpResponseMessage past = await Get(headers => headers.Range = new RangeHeaderValue(file.Length + 5, file.Length + 10));
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, past.StatusCode);

        using HttpResponseMessage unchanged = await Get(headers => headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{summary.Sha256}\"")));
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);

        using HttpResponseMessage staleValidator = await Get(headers =>
        {
            headers.Range = new RangeHeaderValue(0, 9);
            headers.IfRange = new RangeConditionHeaderValue(new EntityTagHeaderValue("\"other\""));
        });
        Assert.Equal(HttpStatusCode.OK, staleValidator.StatusCode);
        Assert.Equal(file, await staleValidator.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task AnUploadThatIsNotAnAcceptedRequestOrFileIsRefusedAndLeavesNothingOnDisk()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        byte[] file = Wav();

        using HttpResponseMessage wrongType = await UploadAsync(server, file, type: "audio/mpeg");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
        using HttpResponseMessage noType = await server.Client.PostAsync("/api/media?name=a.wav", new ByteArrayContent(file));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, noType.StatusCode);
        using HttpResponseMessage noName = await server.Client.PostAsync("/api/media", WavBody(file));
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        using HttpResponseMessage twoNames = await server.Client.PostAsync("/api/media?name=a&name=b", WavBody(file));
        Assert.Equal(HttpStatusCode.BadRequest, twoNames.StatusCode);
        using HttpResponseMessage blankName = await UploadAsync(server, file, " ");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blankName.StatusCode);
        using HttpResponseMessage empty = await UploadAsync(server, []);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, empty.StatusCode);
        using HttpResponseMessage truncated = await UploadAsync(server, file[..^3]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, truncated.StatusCode);
        Assert.Contains("truncated", await truncated.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using HttpResponseMessage notWav = await UploadAsync(server, "ID3\u0003 this is an mp3"u8.ToArray());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notWav.StatusCode);
        byte[] adpcm = TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt(2, 1, 48000, 96000, 2, 16)), TestWav.Chunk("data", new byte[40]));
        using HttpResponseMessage compressed = await UploadAsync(server, adpcm);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, compressed.StatusCode);
        Assert.Contains("16, 24 or 32-bit PCM", await compressed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Empty(Media(server));
    }

    [Fact]
    public async Task AnOversizedUploadIsRefusedWhetherOrNotTheClientAnnouncesItsLength()
    {
        await using RunningServer server = await StartWith("AudioLab:Media:MaxUploadBytes", "2048");
        byte[] big = Wav(2000);
        Assert.True(big.Length > 2048);

        // The server may close the connection before the client has sent its body, so a reset is as good as the 413.
        HttpStatusCode? announced = await StatusOrResetAsync(() => UploadAsync(server, big));
        Assert.True(announced is null or HttpStatusCode.RequestEntityTooLarge, $"announced: {announced}");

        using ChunkedContent chunked = new(big);
        chunked.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        HttpStatusCode? unannounced = await StatusOrResetAsync(() => server.Client.PostAsync("/api/media?name=big.wav", chunked));
        Assert.True(unannounced is null or HttpStatusCode.RequestEntityTooLarge, $"unannounced: {unannounced}");
        Assert.Empty(Media(server));

        byte[] fits = Wav(400);
        Assert.True(fits.Length <= 2048);
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(server, fits)).StatusCode);
        Assert.Equal(3, Media(server).Length);
    }

    [Fact]
    public async Task AUploadLargerThanTheGlobalBodyLimitIsAcceptedBecauseTheRouteHasItsOwn()
    {
        await using RunningServer server = await StartWith("AudioLab:MaxRequestBodyBytes", "2048");
        byte[] file = Wav(48000);
        Assert.True(file.Length > 100_000);
        MediaSummary summary = await UploadedAsync(server, file);
        Assert.Equal(file.Length, summary.Bytes);
    }

    [Fact]
    public async Task AnotherUsersFileIsNeverFoundNeverListedAndNeverNamedInAnAnswer()
    {
        await using RunningServer server = await RunningServer.StartAsync(Swarm());
        MediaSummary alices = await UploadedAsync(server, Wav(), "alice.wav", "alice");
        UserId alice = UserId.ForDoor("swarm", "alice");

        using HttpResponseMessage missing = await SendAsync(server, HttpMethod.Get, "/api/media/med_nothing", "bob");
        string missingBody = await missing.Content.ReadAsStringAsync();
        (HttpMethod, string)[] routes = [(HttpMethod.Get, string.Empty), (HttpMethod.Get, "/content"), (HttpMethod.Delete, string.Empty)];
        foreach ((HttpMethod method, string suffix) in routes)
        {
            using HttpResponseMessage theirs = await SendAsync(server, method, $"/api/media/{alices.Id}{suffix}", "bob");
            using HttpResponseMessage nothing = await SendAsync(server, method, $"/api/media/med_nothing{suffix}", "bob");
            Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
            Assert.Equal(nothing.StatusCode, theirs.StatusCode);
            string body = await theirs.Content.ReadAsStringAsync();
            Assert.DoesNotContain(alices.Id, body, StringComparison.Ordinal);
            Assert.DoesNotContain(alice.Value, body, StringComparison.Ordinal);
            Assert.Equal(StripStatusDetail(await nothing.Content.ReadAsStringAsync()), StripStatusDetail(body));
        }
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.DoesNotContain(alices.Id, missingBody, StringComparison.Ordinal);

        Assert.Empty((await ListAsync(server, "bob")).Files);
        Assert.Single((await ListAsync(server, "alice")).Files);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(server, HttpMethod.Get, $"/api/media/{alices.Id}/content", "alice")).StatusCode);
    }

    private static async Task<MediaListing> ListAsync(RunningServer server, string user)
    {
        using HttpResponseMessage response = await SendAsync(server, HttpMethod.Get, "/api/media", user);
        return (await response.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaListing))!;
    }

    // The traceId a problem answer carries differs between requests; nothing else may.
    private static string StripStatusDetail(string body) => Regex.Replace(body, "\"traceId\":\"[^\"]*\"", string.Empty);

    [Fact]
    public async Task EveryMediaRouteIsBehindTheDoorAndOnlyHealthIsOutsideIt()
    {
        await using RunningServer server = await RunningServer.StartAsync(Swarm());
        foreach ((HttpMethod method, string path) in new[]
        {
            (HttpMethod.Get, "/api/media"),
            (HttpMethod.Post, "/api/media?name=a.wav"),
            (HttpMethod.Get, "/api/media/med_1"),
            (HttpMethod.Get, "/api/media/med_1/content"),
            (HttpMethod.Get, "/api/media/med_1/peaks"),
            (HttpMethod.Delete, "/api/media/med_1"),
        })
        {
            HttpContent? body = method == HttpMethod.Post ? WavBody(Wav(100)) : null;
            using HttpResponseMessage noSecret = await server.Client.SendAsync(new HttpRequestMessage(method, path) { Content = body });
            Assert.Equal(HttpStatusCode.Forbidden, noSecret.StatusCode);
            using HttpRequestMessage secretOnly = new(method, path);
            secretOnly.Headers.Add("X-AudioLab-Secret", Secret);
            using HttpResponseMessage noUser = await server.Client.SendAsync(secretOnly);
            Assert.Equal(HttpStatusCode.Unauthorized, noUser.StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync("/health")).StatusCode);
        Assert.Empty(Media(server));
    }

    [Fact]
    public async Task TheDesktopDoorRefusesAForeignOriginOnAnUpload()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/media?name=a.wav", content: WavBody(Wav(100)));
        request.Headers.Add("Origin", "https://evil.example");
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(Media(server));
    }

    [Theory]
    [InlineData("..%2F..%2Fsecret")]
    [InlineData("%2e%2e")]
    [InlineData("..")]
    [InlineData("CON")]
    [InlineData("MED_UPPER")]
    [InlineData("a%00b")]
    [InlineData("a%5Cb")]
    [InlineData("%2Fetc%2Fpasswd")]
    [InlineData("C%3A%5CWindows")]
    public async Task AnIdThatIsNotAnIdIsRefusedAndTouchesNothing(string hostile)
    {
        using TempFolder parent = new();
        string root = Path.Combine(parent.Path, "store");
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:Storage:Root"] = root });
        string[] before = Directory.GetFileSystemEntries(parent.Path);
        foreach (string suffix in new[] { string.Empty, "/content" })
        {
            foreach (HttpMethod method in new[] { HttpMethod.Get, HttpMethod.Delete })
            {
                using HttpResponseMessage response = await SendAsync(server, method, $"/api/media/{hostile}{suffix}");
                bool refused = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed;
                Assert.True(refused, $"{method} {hostile}{suffix} answered {(int)response.StatusCode}");
            }
        }
        Assert.Equal(before.Order(), Directory.GetFileSystemEntries(parent.Path).Order());
    }

    [Fact]
    public async Task AHostileNameIsOnlyALabelAndNothingIsWrittenOutsideTheStore()
    {
        using TempFolder parent = new();
        string root = Path.Combine(parent.Path, "store");
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:Storage:Root"] = root });
        MediaSummary summary = await UploadedAsync(server, Wav(), "../../../outside.wav");
        Assert.Equal("../../../outside.wav", summary.Name);
        Assert.Equal(["store"], Directory.GetFileSystemEntries(parent.Path).Select(entry => Path.GetFileName(entry)).ToArray());
        IEnumerable<string> all = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
        foreach (string file in all.Where(file => !file.EndsWith("store.lock", StringComparison.Ordinal)))
        {
            Assert.Matches(@"^users/u[0-9a-f]{32}/media/m[0-9a-f]{32}\.(json|wav|peaks)$", Path.GetRelativePath(root, file).Replace('\\', '/'));
        }
    }

    [Fact]
    public async Task ConcurrentUploadsOfOneFileOverHttpAreAllStoredIntact()
    {
        await using RunningServer server = await StartWith("AudioLab:Media:MaxConcurrentUploadsPerUser", "16");
        byte[] file = Wav(48000);
        Task<MediaSummary>[] uploads = [.. Enumerable.Range(0, 8).Select(index => Task.Run(() => UploadedAsync(server, file, $"same-{index}.wav")))];
        MediaSummary[] summaries = await Task.WhenAll(uploads);
        Assert.Equal(8, summaries.Select(summary => summary.Id).Distinct().Count());
        foreach (MediaSummary summary in summaries)
        {
            Assert.Equal(file, await server.Client.GetByteArrayAsync($"/api/media/{summary.Id}/content"));
        }
    }

    [Fact]
    public async Task AFileAProjectPlaysIsAnswered409ToADeleteAndGoesOnceTheProjectLetsGo()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        MediaSummary summary = await UploadedAsync(server, Wav());
        ProjectService projects = server.Services.GetRequiredService<ProjectService>();
        ProjectEditService edits = server.Services.GetRequiredService<ProjectEditService>();
        ProjectId project = ProjectId.From((await projects.CreateAsync(UserId.Local, "Uses it", CancellationToken.None)).Id);
        ClientId client = ClientId.From("c1");

        async Task<CommandReply> Apply(string name, string members)
        {
            string json = $"{{\"project\":\"{project.Value}\",{members}}}";
            return await edits.ApplyAsync(UserId.Local, project, client, name, json, null, CancellationToken.None);
        }

        string track = (await Apply("track_add", "\"kind\":\"audio\"")).Created.Single();
        string duration = summary.Duration.ToString("R", CultureInfo.InvariantCulture);
        string add = $"\"track\":\"{track}\",\"media\":\"{summary.Id}\",\"duration\":{duration}";
        string clip = (await Apply("clip_add", add)).Created.Single();

        using HttpResponseMessage refused = await server.Client.DeleteAsync($"/api/media/{summary.Id}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Uses it", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await server.Client.GetAsync($"/api/media/{summary.Id}/content")).StatusCode);

        Assert.True((await Apply("clip_delete", $"\"clip\":\"{clip}\"")).Succeeded);
        await projects.SaveAsync(UserId.Local, project, CancellationToken.None);
        await server.Services.GetRequiredService<AudioLab.Server.Store.OpenProjects>().CloseAsync(UserId.Local, project, CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, (await server.Client.DeleteAsync($"/api/media/{summary.Id}")).StatusCode);
    }

    [Fact]
    public async Task AFullMediaQuotaIsAnswered409WithNoPath()
    {
        await using RunningServer server = await StartWith("AudioLab:Media:MaxFilesPerUser", "1");
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(server, Wav(100))).StatusCode);
        using HttpResponseMessage full = await UploadAsync(server, Wav(100));
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        string text = await full.Content.ReadAsStringAsync();
        Assert.DoesNotContain(server.StoreFolder, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("MaxUploadBytes", "10")]
    [InlineData("MaxFilesPerUser", "0")]
    [InlineData("MaxBytesPerUser", "2048")]
    [InlineData("MaxUploadBytes", "4294967305")]
    [InlineData("MaxConcurrentUploadsPerUser", "0")]
    public void AMediaConfigurationTheServerCannotRunWithIsRefusedAtStart(string key, string value)
    {
        using TempFolder folder = new();
        Dictionary<string, string?> settings = new() { ["AudioLab:Storage:Root"] = folder.Path, [$"AudioLab:Media:{key}"] = value };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ServerHost.Build([], settings));
        Assert.Contains($"AudioLab:Media:{key}", error.Message, StringComparison.Ordinal);
    }

    private sealed class ChunkedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes, 0, bytes.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
