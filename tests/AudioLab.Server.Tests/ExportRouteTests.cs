using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static AudioLab.Server.Tests.ExportTestSupport;

namespace AudioLab.Server.Tests;

public sealed class ExportRouteTests
{
    private static readonly UserId _local = UserId.Local;

    // 0.5 s of 48 kHz stereo at a constant 0.25 on both channels, so a sample of the export says what the engine made of it.
    private static byte[] Constant(double seconds = 0.5, int rate = 48000, int channels = 2) =>
        TestWav.Build(SampleEncoding.Pcm16, channels, rate, (int)(seconds * rate), (_, _) => 0.25);

    private static async Task<byte[]> BytesAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes.Length, response.Content.Headers.ContentLength);
        return bytes;
    }

    private static async Task<(ProjectId Project, MediaSummary Media)> OneClipProjectAsync(
        RunningServer server,
        byte[]? file = null,
        string name = "Song")
    {
        MediaSummary media = await UploadAsync(server, file ?? Constant());
        ProjectId project = await CreateProjectAsync(server, name);
        _ = await AddClipAsync(server, _local, project, media);
        return (project, media);
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    // The same answer apart from the id the framework gives each request.
    private static async Task<string> WithoutTraceAsync(HttpResponseMessage response)
    {
        JsonElement problem = await ProblemAsync(response);
        IEnumerable<string> members = problem.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .Select(property => $"{property.Name}={property.Value}");
        return string.Join("|", members);
    }

    private static void AssertNoFileHeaders(HttpResponseMessage response)
    {
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.NotEqual("audio/wav", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEqual("application/zip", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("pcm16", 1, 16, 2, 44100)]
    [InlineData("pcm24", 1, 24, 3, 48000)]
    [InlineData("float32", 3, 32, 4, 96000)]
    public async Task AMixIsAWavFileInTheFormatAndRateAskedFor(string format, int tag, int bits, int bytes, int rate)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server);

        string request = $"{{\"format\":\"{format}\",\"sampleRate\":{rate},\"tail\":false}}";

        using HttpResponseMessage response = await ExportAsync(server, project, "mix", request);

        Assert.Equal("audio/wav", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("Song.wav", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        WavInfo wav = WavInfo.Parse(await BytesAsync(response));
        Assert.Equal((tag, 2, rate, bits), (wav.Tag, wav.Channels, wav.Rate, wav.Bits));
        Assert.Equal(rate / 2, wav.Frames);
        Assert.Equal(bits / 8, bytes);
        // A constant of 0.25 at unity, after the engine's resampler, in the middle of the file.
        Assert.Equal(0.25, wav.Sample(wav.Frames / 2, 0), 2e-3);
        Assert.Equal(wav.Sample(wav.Frames / 2, 0), wav.Sample(wav.Frames / 2, 1));
    }

    [Fact]
    public async Task ARangeAndATailAreFollowedAndTheSameRequestGivesTheSameBytes()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server);
        const string Range = "{\"format\":\"pcm16\",\"start\":0.1,\"end\":0.4,\"tail\":true,\"tailSeconds\":0.25,\"seed\":7}";

        using HttpResponseMessage first = await ExportAsync(server, project, "mix", Range);
        using HttpResponseMessage second = await ExportAsync(server, project, "mix", Range);

        byte[] bytes = await BytesAsync(first);
        Assert.Equal(bytes, await BytesAsync(second));
        Assert.Equal((int)Math.Round(0.3 * 48000) + (int)(0.25 * 48000), WavInfo.Parse(bytes).Frames);
    }

    [Fact]
    public async Task TheDitherIsTheOneAskedForAndAFloatFileHasNone()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server);

        async Task<byte[]> Export(string format, string dither)
        {
            string request = $"{{\"format\":\"{format}\",\"dither\":\"{dither}\",\"tail\":false}}";
            using HttpResponseMessage response = await ExportAsync(server, project, "mix", request);
            return WavInfo.Parse(await BytesAsync(response)).Data;
        }

        Assert.NotEqual(await Export("pcm16", "none"), await Export("pcm16", "triangular"));
        Assert.NotEqual(await Export("pcm16", "rectangular"), await Export("pcm16", "triangular"));
        Assert.Equal(await Export("float32", "none"), await Export("float32", "triangular"));
    }

    [Fact]
    public async Task StemsAreAZipOfWavFilesNamedByPlaceAndTrack()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        MediaSummary loud = await UploadAsync(server, Constant());
        MediaSummary quiet = await UploadAsync(server, TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 24000, (_, _) => 0.125));
        ProjectId project = await CreateProjectAsync(server, "Stem song");
        _ = await AddClipAsync(server, _local, project, loud, "Kick");
        _ = await AddClipAsync(server, _local, project, quiet, "Snare");

        using HttpResponseMessage response = await ExportAsync(server, project, "stems", "{\"format\":\"pcm16\",\"tail\":false}");

        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Stem song stems.zip", response.Content.Headers.ContentDisposition?.FileNameStar);
        using ZipArchive zip = new(new MemoryStream(await BytesAsync(response)));
        Assert.Equal(["01 Kick.wav", "02 Snare.wav"], zip.Entries.Select(entry => entry.FullName));
        double[] expected = [0.25, 0.125];
        for (int index = 0; index < zip.Entries.Count; index++)
        {
            using MemoryStream entry = new();
            await using (Stream source = await zip.Entries[index].OpenAsync())
            {
                await source.CopyToAsync(entry);
            }
            WavInfo wav = WavInfo.Parse(entry.ToArray());
            Assert.Equal(24000, wav.Frames);
            Assert.Equal(expected[index], wav.Sample(wav.Frames / 2, 0), 2e-3);
        }
    }

    [Fact]
    public async Task OnlyTheTracksNamedAreMadeIntoStems()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        MediaSummary media = await UploadAsync(server, Constant());
        ProjectId project = await CreateProjectAsync(server);
        _ = await AddClipAsync(server, _local, project, media, "One");
        string second = await AddClipAsync(server, _local, project, media, "Two");

        using HttpResponseMessage response = await ExportAsync(server, project, "stems", $"{{\"tracks\":[\"{second}\"],\"tail\":false}}");

        using ZipArchive zip = new(new MemoryStream(await BytesAsync(response)));
        Assert.Equal(["01 Two.wav"], zip.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public async Task HostileNamesNeverReachAFileNameOrAZipEntryAsAPath()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        MediaSummary media = await UploadAsync(server, Constant());
        string hostile = "\"; filename=evil.exe; x=\"..\\..\\a/b";
        ProjectId project = await CreateProjectAsync(server, hostile);
        _ = await AddClipAsync(server, _local, project, media, "../../etc/passwd");
        _ = await AddClipAsync(server, _local, project, media, "CON");

        using HttpResponseMessage mix = await ExportAsync(server, project, "mix", "{\"tail\":false}");
        using HttpResponseMessage stems = await ExportAsync(server, project, "stems", "{\"tail\":false}");

        ContentDispositionHeaderValue disposition = mix.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.Equal(["filename", "filename*"], disposition.Parameters.Select(parameter => parameter.Name).Order());
        Assert.Equal("_; filename=evil.exe; x=_.._.._a_b.wav", disposition.FileNameStar);
        Assert.DoesNotContain('/', disposition.FileNameStar!);
        Assert.DoesNotContain('\\', disposition.FileNameStar!);
        Assert.DoesNotContain('"', disposition.FileNameStar!);
        using ZipArchive zip = new(new MemoryStream(await BytesAsync(stems)));
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            Assert.Equal(entry.Name, entry.FullName);
            Assert.DoesNotContain('/', entry.FullName);
            Assert.DoesNotContain('\\', entry.FullName);
        }
        Assert.Equal(["01 .._.._etc_passwd.wav", "02 CON.wav"], zip.Entries.Select(entry => entry.FullName));
    }

    [Theory]
    [InlineData("con", "_con.wav")]
    [InlineData("Nul.take", "_Nul.take.wav")]
    public async Task AProjectNamedForADeviceStillGetsAUsableFileName(string name, string expected)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server, name: name);

        using HttpResponseMessage response = await ExportAsync(server, project, "mix", "{\"tail\":false}");

        Assert.Equal(expected, response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Fact]
    public async Task AProjectOfAnotherUserAndOneThatDoesNotExistAreTheSameNotFound()
    {
        await using RunningServer server = await RunningServer.StartAsync(Swarm());
        ProjectId bobs = await CreateProjectAsync(server, "Bob's", "bob");

        using HttpResponseMessage foreign = await ExportAsync(server, bobs, "mix", user: "alice");
        using HttpResponseMessage missing = await ExportAsync(server, ProjectId.From("no-such-project"), "mix", user: "alice");
        using HttpResponseMessage stems = await ExportAsync(server, bobs, "stems", user: "alice");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stems.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await WithoutTraceAsync(missing), await WithoutTraceAsync(foreign));
        AssertNoFileHeaders(foreign);
    }

    [Fact]
    public async Task AProjectNamingAnotherUsersMediaIsRefusedLikeOneNamingNothingAndNamesOnlyItsOwnIds()
    {
        await using RunningServer server = await RunningServer.StartAsync(Swarm());
        UserId alice = ProxyDoor.UserFor("swarm", "alice");
        MediaSummary mine = await UploadAsync(server, Constant(), "alice");
        MediaSummary bobs = await UploadAsync(server, Constant(), "bob");
        ProjectId project = await CreateProjectAsync(server, "Mixed up", "alice");
        _ = await AddClipAsync(server, alice, project, mine);

        async Task<ProblemOf> ExportNamingAsync(string media)
        {
            await Rename(server, alice, project, media);
            using HttpResponseMessage response = await ExportAsync(server, project, "mix", user: "alice");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            AssertNoFileHeaders(response);
            JsonElement problem = await ProblemAsync(response);
            string[] details = [.. problem.GetProperty("details").EnumerateArray().Select(item => item.GetString()!)];
            return new ProblemOf(problem.GetProperty("reason").GetString()!, problem.GetProperty("title").GetString()!, details);
        }

        ProblemOf foreign = await ExportNamingAsync(bobs.Id);
        ProblemOf nothing = await ExportNamingAsync("never-uploaded");

        Assert.Equal("missing_media", foreign.Reason);
        Assert.Equal(foreign.Reason, nothing.Reason);
        Assert.Equal(foreign.Title, nothing.Title);
        Assert.Single(foreign.Details);
        Assert.Contains(bobs.Id, foreign.Details[0], StringComparison.Ordinal);
        Assert.Contains("never-uploaded", nothing.Details[0], StringComparison.Ordinal);
        Assert.DoesNotContain("bob", foreign.Details[0], StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ProblemOf(string Reason, string Title, string[] Details);

    // A command cannot name a file the user lacks, so a project that does is made by writing the stored file, as a damaged or edited one would be.
    private static async Task Rename(RunningServer server, UserId user, ProjectId project, string media)
    {
        AudioLab.Server.Store.OpenProjects open = server.Services.GetRequiredService<AudioLab.Server.Store.OpenProjects>();
        AudioLab.Server.Store.ProjectStore store = server.Services.GetRequiredService<AudioLab.Server.Store.ProjectStore>();
        await open.CloseAsync(user, project, CancellationToken.None);
        ProjectDocument document = await store.ReadAsync(user, project, CancellationToken.None);
        Track Retarget(Track track) => track with { Clips = [.. track.Clips.Select(clip => clip with { Media = MediaId.From(media) })] };
        ProjectDocument edited = document with { Tracks = [.. document.Tracks.Select(Retarget)] };
        await store.WriteAsync(edited, CancellationToken.None);
    }

    [Theory]
    [InlineData("{\"sampleRate\":12345}")]
    [InlineData("{\"start\":-1}")]
    [InlineData("{\"start\":2,\"end\":1}")]
    [InlineData("{\"end\":0}")]
    [InlineData("{\"tail\":false,\"tailSeconds\":1}")]
    [InlineData("{\"tailSeconds\":1000}")]
    [InlineData("{\"tailSeconds\":-1}")]
    [InlineData("{\"tracks\":[\"a\"]}")]
    [InlineData("{\"format\":\"mp3\"}")]
    [InlineData("{\"dither\":1}")]
    [InlineData("{\"surprise\":true}")]
    [InlineData("{\"seed\":-1}")]
    [InlineData("[]")]
    [InlineData("{\"sampleRate\":\"48000\"}")]
    public async Task AnOptionThatIsWrongIsRefusedBeforeAnythingIsRendered(string json)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server);

        using HttpResponseMessage response = await ExportAsync(server, project, "mix", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_options", (await ProblemAsync(response)).GetProperty("reason").GetString());
        AssertNoFileHeaders(response);
    }

    [Fact]
    public async Task OptionsThatNameAStemTrackThatIsNotThereOrNamedTwiceAreRefused()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server);

        using HttpResponseMessage unknown = await ExportAsync(server, project, "stems", "{\"tracks\":[\"ghost\"]}");
        using HttpResponseMessage twice = await ExportAsync(server, project, "stems", "{\"tracks\":[\"a\",\"a\"]}");
        using HttpResponseMessage none = await ExportAsync(server, project, "stems", "{\"tracks\":[]}");

        foreach (HttpResponseMessage response in new[] { unknown, twice, none })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("bad_options", (await ProblemAsync(response)).GetProperty("reason").GetString());
        }
    }

    [Fact]
    public async Task ABodyThatIsNotJsonIsRefused()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server);
        using StringContent body = new("format=pcm16", System.Text.Encoding.UTF8, "text/plain");
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/projects/{project.Value}/export/mix", null, body);

        using HttpResponseMessage response = await server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task AnEmptyBodyMeansTheDefaultsAndAProjectWithNoSoundIsRefused()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        ProjectId empty = await CreateProjectAsync(server, "Silence");
        (ProjectId project, _) = await OneClipProjectAsync(server);
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/projects/{project.Value}/export/mix");

        using HttpResponseMessage defaults = await server.Client.SendAsync(request);
        using HttpResponseMessage mix = await ExportAsync(server, empty, "mix");
        using HttpResponseMessage stems = await ExportAsync(server, empty, "stems");

        WavInfo wav = WavInfo.Parse(await BytesAsync(defaults));
        Assert.Equal((24, 48000), (wav.Bits, wav.Rate));
        foreach (HttpResponseMessage response in new[] { mix, stems })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("empty", (await ProblemAsync(response)).GetProperty("reason").GetString());
            AssertNoFileHeaders(response);
        }
    }

    [Theory]
    [InlineData("MaxSeconds", "0.25", "mix")]
    [InlineData("MaxBytes", "1048576", "stems")]
    public async Task AnExportOverALimitIsRefusedBeforeItsFirstByte(string option, string value, string kind)
    {
        // 2 s of 96 kHz float stereo is 1.5 MB, over the 1 MiB limit, and 2 s is over a quarter of a second.
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { [$"AudioLab:Render:{option}"] = value });
        (ProjectId project, _) = await OneClipProjectAsync(server, Constant(seconds: 2));

        using HttpResponseMessage response = await ExportAsync(server, project, kind, "{\"format\":\"float32\",\"sampleRate\":96000,\"tail\":false}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("too_large", (await ProblemAsync(response)).GetProperty("reason").GetString());
        AssertNoFileHeaders(response);
        Assert.Equal(0, server.Services.GetRequiredKeyedService<ConcurrencyGate>(GateKeys.Export).Running);
    }

    [Fact]
    public async Task AFileTooLongToDecodeIsRefusedAsTooLarge()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Render:MaxDecodedBytesPerFile"] = "1048576" };
        await using RunningServer server = await RunningServer.StartAsync(settings);
        (ProjectId project, _) = await OneClipProjectAsync(server, Constant(seconds: 3));

        using HttpResponseMessage response = await ExportAsync(server, project, "mix");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("too_large", (await ProblemAsync(response)).GetProperty("reason").GetString());
    }

    // Long enough that its bytes cannot all sit in socket buffers: 300 s of 96 kHz float stereo is 230 MB.
    private static async Task<(ProjectId Project, MediaSummary Media)> LongProjectAsync(RunningServer server)
    {
        byte[] silence = TestWav.Build(SampleEncoding.Pcm16, 1, 8000, 300 * 8000, (_, _) => 0);
        return await OneClipProjectAsync(server, silence, "Long");
    }

    private const string Huge = "{\"format\":\"float32\",\"sampleRate\":96000,\"tail\":false}";

    [Fact]
    public async Task AUserOverTheirLimitIsToldToWaitAndAnotherUserIsNot()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?>(Swarm())
            {
                ["AudioLab:Render:MaxConcurrentExportsPerUser"] = "1",
                ["AudioLab:Render:MaxConcurrentExports"] = "8",
            });
        UserId alice = ProxyDoor.UserFor("swarm", "alice");
        UserId bob = ProxyDoor.UserFor("swarm", "bob");
        MediaSummary aliceMedia = await UploadAsync(server, TestWav.Build(SampleEncoding.Pcm16, 1, 8000, 300 * 8000, (_, _) => 0), "alice");
        ProjectId aliceProject = await CreateProjectAsync(server, "A", "alice");
        _ = await AddClipAsync(server, alice, aliceProject, aliceMedia);
        MediaSummary bobMedia = await UploadAsync(server, Constant(), "bob");
        ProjectId bobProject = await CreateProjectAsync(server, "B", "bob");
        _ = await AddClipAsync(server, bob, bobProject, bobMedia);
        ConcurrencyGate gate = server.Services.GetRequiredKeyedService<ConcurrencyGate>(GateKeys.Export);

        using HttpResponseMessage running = await ExportAsync(server, aliceProject, "mix", Huge, "alice", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, running.StatusCode);
        using HttpResponseMessage second = await ExportAsync(server, aliceProject, "mix", Huge, "alice");
        using HttpResponseMessage others = await ExportAsync(server, bobProject, "mix", user: "bob");

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("busy", (await ProblemAsync(second)).GetProperty("reason").GetString());
        Assert.Equal("5", second.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(HttpStatusCode.OK, others.StatusCode);
        await WaitForAsync(() => gate.Running == 1);

        running.Dispose();
        await WaitForAsync(() => gate.Running == 0);
        using HttpResponseMessage again = await ExportAsync(server, aliceProject, "mix", Huge, "alice", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task TheServerAsAWholeRunsOnlyAsManyExportsAsItAllows()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?>
            {
                ["AudioLab:Render:MaxConcurrentExportsPerUser"] = "4",
                ["AudioLab:Render:MaxConcurrentExports"] = "1",
            });
        (ProjectId project, _) = await LongProjectAsync(server);
        using HttpResponseMessage running = await ExportAsync(server, project, "mix", Huge, completion: HttpCompletionOption.ResponseHeadersRead);

        using HttpResponseMessage refused = await ExportAsync(server, project, "mix", Huge);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("busy", (await ProblemAsync(refused)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ANameWithLettersBeyondAsciiKeepsItsNameInTheDownload()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server, name: "Café ♪ 日本");

        using HttpResponseMessage response = await ExportAsync(server, project, "mix", "{\"tail\":false}");

        Assert.Equal("Café ♪ 日本.wav", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnExportThatIsTooLongIsRefusedByItsLengthBeforeAnyFileIsDecoded()
    {
        // The file is also over the decode limit: had it been read first, the answer would have been about the file.
        Dictionary<string, string?> settings = new()
        {
            ["AudioLab:Render:MaxSeconds"] = "1",
            ["AudioLab:Render:MaxDecodedBytesPerFile"] = "1048576",
        };
        await using RunningServer server = await RunningServer.StartAsync(settings);
        (ProjectId project, _) = await OneClipProjectAsync(server, Constant(seconds: 3));

        using HttpResponseMessage response = await ExportAsync(server, project, "mix");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("seconds the server allows", (await ProblemAsync(response)).GetProperty("title").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDetailsOfARefusalAreAlwaysAListOfSentencesAndAbsentWhenThereIsNothingToList()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await OneClipProjectAsync(server);

        using HttpResponseMessage unknown = await ExportAsync(server, project, "stems", "{\"tracks\":[\"ghost\"]}");
        using HttpResponseMessage plain = await ExportAsync(server, project, "mix", "{\"sampleRate\":1}");

        JsonElement details = (await ProblemAsync(unknown)).GetProperty("details");
        Assert.Equal(JsonValueKind.Array, details.ValueKind);
        Assert.Equal(["track ghost is not in the project"], details.EnumerateArray().Select(item => item.GetString()));
        Assert.False((await ProblemAsync(plain)).TryGetProperty("details", out _));
    }

    [Fact]
    public async Task AClientThatLeavesMidStreamStopsTheRenderAndFreesItsPlace()
    {
        CapturedLogs logs = new();
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?> { ["Logging:LogLevel:AudioLab.Server.Render.ExportErrorFilter"] = "Information" },
            services => services.AddSingleton<ILoggerProvider>(logs));
        (ProjectId project, _) = await LongProjectAsync(server);
        ConcurrencyGate gate = server.Services.GetRequiredKeyedService<ConcurrencyGate>(GateKeys.Export);

        using (HttpResponseMessage response = await ExportAsync(server, project, "mix", Huge, completion: HttpCompletionOption.ResponseHeadersRead))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using Stream body = await response.Content.ReadAsStreamAsync();
            byte[] first = new byte[4096];
            await body.ReadExactlyAsync(first);
            Assert.Equal(1, gate.Running);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(first, 0, 4));
        }

        await WaitForAsync(() => gate.Running == 0);
        // The server noticed that the client had gone, after the first byte, and ended the response by cutting it: its own log says so.
        await WaitForAsync(() => logs.Messages.Any(message => message.Contains("stopped after its first byte", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AnExportThatRunsPastTheTimeLimitIsCutSoNoClientTakesItAsAWholeFile()
    {
        ManualTimeProvider time = new();
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?> { ["AudioLab:Render:MaxTime"] = "00:10:00" },
            services => services.AddSingleton<TimeProvider>(time));
        (ProjectId project, _) = await LongProjectAsync(server);
        ConcurrencyGate gate = server.Services.GetRequiredKeyedService<ConcurrencyGate>(GateKeys.Export);
        using HttpResponseMessage response = await ExportAsync(server, project, "mix", Huge, completion: HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        long promised = response.Content.Headers.ContentLength!.Value;
        await using Stream body = await response.Content.ReadAsStreamAsync();
        byte[] first = new byte[4096];
        await body.ReadExactlyAsync(first);

        time.Advance(TimeSpan.FromMinutes(11));

        long read = first.Length;
        byte[] buffer = new byte[1 << 16];
        await Assert.ThrowsAnyAsync<IOException>(async () =>
        {
            int count;
            while ((count = await body.ReadAsync(buffer)) > 0)
            {
                read += count;
            }
        });
        Assert.True(read < promised, "what arrived is less than the length the file was promised");
        await WaitForAsync(() => gate.Running == 0);
    }

    [Fact]
    public async Task InstrumentTracksAreExportedWithTheLengthPromisedAndNotAsSilence()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        byte[] tone = TestWav.Build(SampleEncoding.Pcm16, 1, 48000, 24000, (frame, _) => Math.Sin(frame * 0.1) * 0.5);
        MediaSummary sample = await UploadAsync(server, tone);
        ProjectId project = await CreateProjectAsync(server, "Instruments");
        string keys = "\"kind\":\"instrument\",\"preset\":\"Organ\",\"name\":\"Keys\"";
        string synth = (await ApplyAsync(server, _local, project, "track_add", keys)).Created.Single();
        string hits = "\"kind\":\"instrument\",\"preset\":\"Sampler\",\"name\":\"Hits\"";
        string sampler = (await ApplyAsync(server, _local, project, "track_add", hits)).Created.Single();
        _ = await ApplyAsync(server, _local, project, "instrument_set_sample", $"\"track\":\"{sampler}\",\"sample\":\"{sample.Id}\"");
        foreach (string track in new[] { synth, sampler })
        {
            string add = $"\"track\":\"{track}\",\"start\":0,\"length\":2";
            string clip = (await ApplyAsync(server, _local, project, "midi_clip_add", add)).Created.Single();
            _ = await ApplyAsync(server, _local, project, "midi_note_add", $"\"clip\":\"{clip}\",\"pitch\":60,\"start\":0.1,\"duration\":0.5");
        }

        using HttpResponseMessage mix = await ExportAsync(server, project, "mix", "{\"format\":\"float32\"}");
        using HttpResponseMessage stems = await ExportAsync(server, project, "stems", "{\"format\":\"float32\"}");

        // The length in the header is what was written, with the tail of the instruments in it, or the connection would have been cut.
        WavInfo wav = WavInfo.Parse(await BytesAsync(mix));
        double peak = Enumerable.Range(0, wav.Frames).Max(frame => Math.Abs(wav.Sample(frame, 0)));
        Assert.True(peak > 0.01, $"the mix of two instrument tracks is silent: peak {peak}");
        using ZipArchive zip = new(new MemoryStream(await BytesAsync(stems)));
        Assert.Equal(["01 Keys.wav", "02 Hits.wav"], zip.Entries.Select(entry => entry.FullName));
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            using MemoryStream bytes = new();
            await using (Stream source = await entry.OpenAsync())
            {
                await source.CopyToAsync(bytes);
            }
            WavInfo stem = WavInfo.Parse(bytes.ToArray());
            Assert.True(Enumerable.Range(0, stem.Frames).Max(frame => Math.Abs(stem.Sample(frame, 0))) > 0.01, $"{entry.Name} is silent");
            Assert.Equal(wav.Frames, stem.Frames);
        }
    }

    [Fact]
    public async Task TheOpenProjectsUnsavedChangesAreWhatIsExported()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, MediaSummary media) = await OneClipProjectAsync(server);
        string track = (await ApplyAsync(server, _local, project, "track_add", "\"kind\":\"audio\"")).Created.Single();
        string length = media.Duration.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        string clip = $"\"track\":\"{track}\",\"media\":\"{media.Id}\",\"duration\":{length},\"start\":1";
        _ = await ApplyAsync(server, _local, project, "clip_add", clip);

        using HttpResponseMessage response = await ExportAsync(server, project, "mix", "{\"format\":\"pcm16\",\"tail\":false}");

        // The second clip starts at one second and lasts half a second, and nothing was saved since.
        Assert.Equal(72000, WavInfo.Parse(await BytesAsync(response)).Frames);
    }
}
