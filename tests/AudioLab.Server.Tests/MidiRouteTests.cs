using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AudioLab.Audio.Midi;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Doors;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using Microsoft.Extensions.DependencyInjection;
using static AudioLab.Server.Tests.ExportTestSupport;

namespace AudioLab.Server.Tests;

public sealed class MidiRouteTests
{
    private static readonly UserId _local = UserId.Local;

    private static MidiNote Note(int id, int pitch, double start, double duration, int velocity = 100) =>
        new(new NoteId(id), pitch, start, duration, velocity);

    private static MidiClip Source(MidiNote[] notes, params MidiControlChange[] controls) => new()
    {
        Id = ClipId.From("clp-source"),
        Name = "Source",
        Start = 0,
        Length = 4,
        NextNoteId = new NoteId(notes.Length + 1),
        Notes = new ValueList<MidiNote>(notes),
        ControlChanges = new ValueList<MidiControlChange>(controls),
        Muted = false,
    };

    private static readonly MidiNote[] _notes = [Note(1, 60, 0, 0.5), Note(2, 64, 0.5, 0.25, 90), Note(3, 67, 1, 1, 1)];

    private static readonly MidiControlChange[] _controls = [new(0, 64, 127), new(2, 64, 0)];

    private static byte[] File(double bpm = 120, params TempoChange[] changes) =>
        MidiFile.Write(Source(_notes, _controls), bpm, changes, new TimeSignature(4, 4));

    private static async Task<(ProjectId Project, string Track)> ProjectWithInstrumentAsync(RunningServer server, string? user = null)
    {
        UserId owner = user is null ? _local : ProxyDoor.UserFor("swarm", user);
        ProjectId project = await CreateProjectAsync(server, "Song", user);
        string track = (await ApplyAsync(server, owner, project, "track_add", "\"kind\":\"instrument\"")).Created.Single();
        return (project, track);
    }

    private static async Task<HttpResponseMessage> ImportAsync(
        RunningServer server,
        ProjectId project,
        string track,
        byte[] body,
        string query = "",
        string? user = null,
        string type = "audio/midi")
    {
        ByteArrayContent content = new(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/projects/{project.Value}/tracks/{track}/midi{query}", user, content);
        return await server.Client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetAsync(RunningServer server, string path, string? user = null)
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, path, user);
        return await server.Client.SendAsync(request);
    }

    private static async Task<ProjectDocument> DocumentAsync(RunningServer server, UserId user, ProjectId project)
    {
        return (await server.Services.GetRequiredService<OpenProjects>().OpenAsync(user, project, CancellationToken.None)).Snapshot.Document;
    }

    private static async Task<HistoryState> HistoryAsync(RunningServer server, UserId user, ProjectId project)
    {
        return await server.Services.GetRequiredService<ProjectEditService>().HistoryAsync(user, project, CancellationToken.None);
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task AFileBecomesAClipWithItsNotesAndEventsAsOneUndoStep()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        HistoryState before = await HistoryAsync(server, _local, project);

        using HttpResponseMessage response = await ImportAsync(server, project, track, File(), "?start=2.5&name=Take%201");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        MidiImportResult result = (await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!;
        Assert.Equal($"/api/projects/{project.Value}/clips/{result.Clip}/midi", response.Headers.Location?.ToString());
        Assert.Equal((3, 2, 120.0, 0), (result.Notes, result.ControlChanges, result.Bpm, result.TempoChanges));
        ProjectDocument document = await DocumentAsync(server, _local, project);
        MidiClip clip = Assert.Single(document.Tracks.Single().MidiClips);
        Assert.Equal(result.Clip, clip.Id.Value);
        Assert.Equal(("Take 1", 2.5), (clip.Name, clip.Start));
        Assert.Equal(Shape(_notes), Shape(clip.Notes));
        Assert.Equal([1, 2, 3], clip.Notes.Select(note => note.Id.Value));
        Assert.Equal(_controls, clip.ControlChanges);
        HistoryState history = await HistoryAsync(server, _local, project);
        Assert.Equal(before.UndoCount + 1, history.UndoCount);
        Assert.True(history.Revision > before.Revision);

        CommandReply undone = await server.Services.GetRequiredService<ProjectEditService>().UndoAsync(_local, project, CancellationToken.None);
        Assert.True(undone.Succeeded);
        Assert.Empty((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    [Fact]
    public async Task TheFilesTempoIsReportedAndTheProjectsTempoIsLeftAlone()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        ProjectDocument before = await DocumentAsync(server, _local, project);
        byte[] file = File(90, new TempoChange(TempoChangeId.From("tc1"), 2, 140));

        using HttpResponseMessage response = await ImportAsync(server, project, track, file);

        MidiImportResult result = (await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!;
        Assert.Equal(90.0, result.Bpm, 1e-3);
        Assert.Equal(1, result.TempoChanges);
        ProjectDocument after = await DocumentAsync(server, _local, project);
        Assert.Equal((before.Bpm, before.TempoMap), (after.Bpm, after.TempoMap));
    }

    [Fact]
    public async Task AClipAndATrackAreExportedAsTheFileTheCodecWritesAndReadBackAsTheSameNotes()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        using HttpResponseMessage imported = await ImportAsync(server, project, track, File(), "?name=Lead");
        string clip = (await imported.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!.Clip;
        ProjectDocument document = await DocumentAsync(server, _local, project);
        byte[] expected = MidiFile.Write(document.Tracks.Single().MidiClips.Single(), document.Bpm, document.TempoMap, document.TimeSignature);

        using HttpResponseMessage byClip = await GetAsync(server, $"/api/projects/{project.Value}/clips/{clip}/midi");
        using HttpResponseMessage byTrack = await GetAsync(server, $"/api/projects/{project.Value}/tracks/{track}/midi");

        foreach (HttpResponseMessage response in new[] { byClip, byTrack })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("audio/midi", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal(expected.Length, response.Content.Headers.ContentLength);
            Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync());
        }
        Assert.Equal("Lead.mid", byClip.Content.Headers.ContentDisposition?.FileNameStar);
        MidiImport again = MidiFile.Read(expected);
        Assert.Equal(Shape(_notes), Shape(again.Notes.Select(note => new MidiNote(default, note.Pitch, note.Start, note.Duration, note.Velocity))));
    }

    [Fact]
    public async Task ATrackWithNoClipOrSeveralIsNotExportedAndTheSeveralAreNamed()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);

        using HttpResponseMessage none = await GetAsync(server, $"/api/projects/{project.Value}/tracks/{track}/midi");
        using HttpResponseMessage first = await ImportAsync(server, project, track, File());
        using HttpResponseMessage second = await ImportAsync(server, project, track, File());
        using HttpResponseMessage several = await GetAsync(server, $"/api/projects/{project.Value}/tracks/{track}/midi");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, none.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, several.StatusCode);
        string firstClip = (await first.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!.Clip;
        string secondClip = (await second.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!.Clip;
        string title = (await ProblemAsync(several)).GetProperty("title").GetString()!;
        Assert.Contains(firstClip, title, StringComparison.Ordinal);
        Assert.Contains(secondClip, title, StringComparison.Ordinal);
    }

    public static TheoryData<string, byte[]> BadFiles()
    {
        byte[] good = File();
        byte[] withTrailing = [.. good, 0, 1, 2];
        byte[] format2 = (byte[])good.Clone();
        format2[9] = 2;
        byte[] declaresTooMuch = (byte[])good.Clone();
        declaresTooMuch[18] = 0x7F;
        return new TheoryData<string, byte[]>
        {
            { "empty", [] },
            { "not midi", "RIFFxxxxWAVE"u8.ToArray() },
            { "header only, cut", good[..10] },
            { "cut in the track", good[..^5] },
            { "bytes after the last chunk", withTrailing },
            { "format 2", format2 },
            { "a track that declares more than the file holds", declaresTooMuch },
            { "zeros", new byte[4096] },
        };
    }

    [Theory]
    [MemberData(nameof(BadFiles))]
    public async Task AFileThatIsNotAnAcceptedMidiFileIsRefusedAndTheProjectIsUntouched(string label, byte[] file)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        ProjectDocument before = await DocumentAsync(server, _local, project);
        HistoryState history = await HistoryAsync(server, _local, project);

        using HttpResponseMessage response = await ImportAsync(server, project, track, file);

        Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity, label);
        Assert.Contains("Not an accepted MIDI file", (await ProblemAsync(response)).GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Equal(before, await DocumentAsync(server, _local, project));
        Assert.Equal(history, await HistoryAsync(server, _local, project));
    }

    [Fact]
    public async Task AFileOverTheLimitIsRefusedWhetherOrNotItsLengthWasAnnounced()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Midi:MaxFileBytes"] = "1024" };
        await using RunningServer server = await RunningServer.StartAsync(settings);
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        byte[] big = [.. File(), .. new byte[2000]];

        using HttpResponseMessage announced = await ImportAsync(server, project, track, big);
        StreamContent chunked = new(new NonSeekable(big));
        chunked.Headers.ContentType = new MediaTypeHeaderValue("audio/midi");
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/projects/{project.Value}/tracks/{track}/midi", null, chunked);
        using HttpResponseMessage unannounced = await server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, announced.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, unannounced.StatusCode);
        Assert.Equal("limit", (await ProblemAsync(announced)).GetProperty("reason").GetString());
        Assert.Equal("limit", (await ProblemAsync(unannounced)).GetProperty("reason").GetString());
        Assert.Empty((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    private sealed class NonSeekable(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();
    }

    [Fact]
    public async Task OnlyAMidiContentTypeIsTaken()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);

        using HttpResponseMessage wrong = await ImportAsync(server, project, track, File(), type: "audio/wav");
        using HttpResponseMessage octet = await ImportAsync(server, project, track, File(), type: "application/octet-stream");
        using HttpResponseMessage alias = await ImportAsync(server, project, track, File(), type: "audio/x-midi");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, octet.StatusCode);
        Assert.Equal(HttpStatusCode.Created, alias.StatusCode);
    }

    [Fact]
    public async Task ATrackThatIsNotInstrumentOrNotThereIsRefusedAndNothingChanges()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, _) = await ProjectWithInstrumentAsync(server);
        string audio = (await ApplyAsync(server, _local, project, "track_add", "\"kind\":\"audio\"")).Created.Single();

        using HttpResponseMessage onAudio = await ImportAsync(server, project, audio, File());
        using HttpResponseMessage onGhost = await ImportAsync(server, project, "ghost", File());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, onAudio.StatusCode);
        Assert.Equal("rejected", (await ProblemAsync(onAudio)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, onGhost.StatusCode);
        Assert.Equal("not_found", (await ProblemAsync(onGhost)).GetProperty("reason").GetString());
        Assert.All((await DocumentAsync(server, _local, project)).Tracks, track => Assert.Empty(track.MidiClips));
    }

    [Fact]
    public async Task AnotherUsersProjectTrackAndClipAreTheSameNotFoundAsOnesThatDoNotExist()
    {
        await using RunningServer server = await RunningServer.StartAsync(Swarm());
        (ProjectId bobs, string bobTrack) = await ProjectWithInstrumentAsync(server, "bob");
        using HttpResponseMessage made = await ImportAsync(server, bobs, bobTrack, File(), user: "bob");
        string bobClip = (await made.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!.Clip;
        (ProjectId mine, _) = await ProjectWithInstrumentAsync(server, "alice");

        using HttpResponseMessage importForeign = await ImportAsync(server, bobs, bobTrack, File(), user: "alice");
        using HttpResponseMessage importGhost = await ImportAsync(server, ProjectId.From("no-such-project"), bobTrack, File(), user: "alice");
        using HttpResponseMessage clipForeign = await GetAsync(server, $"/api/projects/{bobs.Value}/clips/{bobClip}/midi", "alice");
        using HttpResponseMessage clipGhost = await GetAsync(server, $"/api/projects/{bobs.Value}/clips/ghost/midi", "alice");
        using HttpResponseMessage clipInMine = await GetAsync(server, $"/api/projects/{mine.Value}/clips/{bobClip}/midi", "alice");
        using HttpResponseMessage clipMissing = await GetAsync(server, $"/api/projects/{mine.Value}/clips/ghost/midi", "alice");
        using HttpResponseMessage trackForeign = await GetAsync(server, $"/api/projects/{bobs.Value}/tracks/{bobTrack}/midi", "alice");

        HttpResponseMessage[] all = [importForeign, importGhost, clipForeign, clipGhost, clipInMine, clipMissing, trackForeign];
        Assert.All(all, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.Equal(Strip(await clipMissing.Content.ReadAsStringAsync()), Strip(await clipInMine.Content.ReadAsStringAsync()));
        Assert.Single((await DocumentAsync(server, ProxyDoor.UserFor("swarm", "bob"), bobs)).Tracks.Single().MidiClips);
    }

    private static (int, double, double, int)[] Shape(IEnumerable<MidiNote> notes) =>
        [.. notes.Select(note => (note.Pitch, note.Start, note.Duration, note.Velocity))];

    private static string Strip(string problem) => System.Text.RegularExpressions.Regex.Replace(problem, "\"traceId\":\"[^\"]*\"", string.Empty);

    [Theory]
    [InlineData("?start=NaN")]
    [InlineData("?start=Infinity")]
    [InlineData("?start=-Infinity")]
    [InlineData("?start=abc")]
    [InlineData("?start=1&start=2")]
    [InlineData("?name=a&name=b")]
    public async Task AStartThatIsNotAFiniteNumberOrAQueryValueGivenTwiceIsABadRequest(string query)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);

        using HttpResponseMessage response = await ImportAsync(server, project, track, File(), query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    [Theory]
    [InlineData("?start=-1")]
    [InlineData("?start=2000000")]
    public async Task AStartThatIsANumberOutsideTheTimelineIsRefusedByTheRulesOfTheProject(string query)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);

        using HttpResponseMessage response = await ImportAsync(server, project, track, File(), query);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid", (await ProblemAsync(response)).GetProperty("reason").GetString());
        Assert.Empty((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    [Theory]
    [InlineData("x%0d%0aSet-Cookie:%20a=b")]
    [InlineData("tab%09name")]
    public async Task AClipNameWithControlCharactersIsRefusedLikeAnyOtherCommandWould(string name)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);

        using HttpResponseMessage response = await ImportAsync(server, project, track, File(), $"?name={name}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    [Fact]
    public async Task ANameThatLooksLikeAPathIsALabelAndTheExportedFileNameIsNeverAPath()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        using HttpResponseMessage made = await ImportAsync(server, project, track, File(), "?name=..%2F..%5Cevil%22%3B%20x%3D");
        string clip = (await made.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!.Clip;

        using HttpResponseMessage response = await GetAsync(server, $"/api/projects/{project.Value}/clips/{clip}/midi");

        Assert.Equal(".._.._evil_; x=.mid", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(["filename", "filename*"], response.Content.Headers.ContentDisposition!.Parameters.Select(parameter => parameter.Name).Order());
    }

    [Fact]
    public async Task AnImportThatWouldMakeTheProjectTooLargeIsRefusedAndNothingIsAdded()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Storage:MaxFileBytes"] = "20000" };
        await using RunningServer server = await RunningServer.StartAsync(settings);
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        MidiNote[] many = [.. Enumerable.Range(0, 800).Select(index => Note(index + 1, 40 + (index % 40), index * 0.01, 0.05))];
        byte[] file = MidiFile.Write(Source(many) with { Length = 20 }, 120, [], new TimeSignature(4, 4));

        using HttpResponseMessage response = await ImportAsync(server, project, track, file);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("limit", (await ProblemAsync(response)).GetProperty("reason").GetString());
        Assert.Empty((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    [Fact]
    public async Task AFileOfAMillionNotesIsOneImportWithinAGenerousBoundAndExportsBack()
    {
        Dictionary<string, string?> settings = new() { ["AudioLab:Storage:MaxFileBytes"] = "134217728" };
        await using RunningServer server = await RunningServer.StartAsync(settings);
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        MidiNote[] many = [.. Enumerable.Range(0, 1_000_000).Select(index => Note(index + 1, index % 128, index * 0.003, 0.02, 1 + (index % 127)))];
        byte[] file = MidiFile.Write(Source(many) with { Length = 3001 }, 120, [], new TimeSignature(4, 4));

        // One command and one write of the project, so it finishes inside the client's own timeout; a path that is quadratic in the
        // notes would not.
        using HttpResponseMessage response = await ImportAsync(server, project, track, file);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        MidiImportResult result = (await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!;
        Assert.Equal(1_000_000, result.Notes);
        HistoryState history = await HistoryAsync(server, _local, project);
        Assert.Equal("midi_clip_import", history.UndoLabel);
        using HttpResponseMessage exported = await GetAsync(server, $"/api/projects/{project.Value}/clips/{result.Clip}/midi");
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal(1_000_000, MidiFile.Read(await exported.Content.ReadAsByteArrayAsync()).Notes.Count);
    }

    [Fact]
    public async Task AUserOverTheirLimitIsToldToWaitAndWorksAgainWhenAPlaceIsFree()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?> { ["AudioLab:Midi:MaxConcurrentPerUser"] = "1", ["AudioLab:Midi:MaxConcurrent"] = "4" });
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        ConcurrencyGate gate = server.Services.GetRequiredKeyedService<ConcurrencyGate>(GateKeys.Midi);
        IDisposable held = gate.Enter(_local);

        using HttpResponseMessage refused = await ImportAsync(server, project, track, File());
        held.Dispose();
        using HttpResponseMessage allowed = await ImportAsync(server, project, track, File());

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("5", refused.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        Assert.Single((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    [Fact]
    public async Task ASlowUploadHoldsNoPlaceInTheGateAndOneThatNeverFinishesIsTimedOut()
    {
        ManualTimeProvider time = new();
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?>
            {
                ["AudioLab:Midi:MaxConcurrentPerUser"] = "1",
                ["AudioLab:Midi:MaxConcurrent"] = "1",
                ["AudioLab:Midi:MaxReadTime"] = "00:01:00",
            },
            services => services.AddSingleton<TimeProvider>(time));
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        ConcurrencyGate gate = server.Services.GetRequiredKeyedService<ConcurrencyGate>(GateKeys.Midi);
        string path = $"/api/projects/{project.Value}/tracks/{track}/midi";
        using HalfSentPost slow = await HalfSentPost.StartAsync(server, path, 100_000, File()[..10]);
        await Task.Delay(500);

        // The upload has started and holds nothing that another user's work waits for, even with a single place in the whole server.
        Assert.Equal(0, gate.Running);
        using HttpResponseMessage other = await ImportAsync(server, project, track, File());
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);

        time.Advance(TimeSpan.FromMinutes(2));
        string? answer = await slow.StatusLineAsync(TimeSpan.FromSeconds(20));
        Assert.Equal("HTTP/1.1 408 Request Timeout", answer);
        Assert.Single((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    [Fact]
    public async Task ATrackThatIsNotThereIsRefusedBeforeTheBodyIsReadAndAFileTooBigForTheProjectBeforeItIsApplied()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        using (HalfSentPost ghost = await HalfSentPost.StartAsync(server, $"/api/projects/{project.Value}/tracks/ghost/midi", 100_000, File()[..10]))
        {
            // The rest of the body never comes, and the answer does.
            Assert.Equal("HTTP/1.1 404 Not Found", await ghost.StatusLineAsync(TimeSpan.FromSeconds(20)));
        }

        // 700,000 notes cannot fit a 32 MiB project even at the least a note takes, so nothing is made of them.
        MidiNote[] many = [.. Enumerable.Range(0, 700_000).Select(index => Note(index + 1, index % 128, index * 0.004, 0.02, 1 + (index % 127)))];
        byte[] file = MidiFile.Write(Source(many) with { Length = 2801 }, 120, [], new TimeSignature(4, 4));
        using HttpResponseMessage tooBig = await ImportAsync(server, project, track, file);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        JsonElement problem = await ProblemAsync(tooBig);
        Assert.Equal("limit", problem.GetProperty("reason").GetString());
        Assert.Contains("at least", problem.GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Empty((await DocumentAsync(server, _local, project)).Tracks.Single().MidiClips);
    }

    // A request whose body never finishes, written to the socket by hand: the client library does not read an answer while it is still
    // sending, and what these tests need to see is the server answering without waiting for the rest.
    private sealed class HalfSentPost : IDisposable
    {
        private readonly System.Net.Sockets.TcpClient _client = new();
        private System.Net.Sockets.NetworkStream? _stream;

        public static async Task<HalfSentPost> StartAsync(RunningServer server, string path, int announced, byte[] sent)
        {
            HalfSentPost post = new();
            Uri address = server.Client.BaseAddress!;
            await post._client.ConnectAsync(address.Host, address.Port);
            post._stream = post._client.GetStream();
            string host = $"{address.Host}:{address.Port}";
            string head = $"POST {path} HTTP/1.1\r\nHost: {host}\r\nContent-Type: audio/midi\r\nContent-Length: {announced}\r\n\r\n";
            await post._stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(head));
            await post._stream.WriteAsync(sent);
            await post._stream.FlushAsync();
            return post;
        }

        // The status line of the answer, or null when none came in the time.
        public async Task<string?> StatusLineAsync(TimeSpan within)
        {
            using CancellationTokenSource timeout = new(within);
            byte[] buffer = new byte[64];
            try
            {
                int read = await _stream!.ReadAsync(buffer, timeout.Token);
                return System.Text.Encoding.ASCII.GetString(buffer, 0, read).Split('\r')[0];
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public void Dispose() => _client.Dispose();
    }

    // A body that sends its first bytes and then waits, so that an import is running while another asks.
    private sealed class Stalling(byte[] file, Task release) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            await stream.WriteAsync(file.AsMemory(0, 10));
            await stream.FlushAsync();
            await release;
            await stream.WriteAsync(file.AsMemory(10));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = file.Length;
            return true;
        }
    }

    [Fact]
    public async Task EveryClientOfTheProjectHearsTheImportAndItsUndoStepIsNamedAfterTheCommand()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        (ProjectId project, string track) = await ProjectWithInstrumentAsync(server);
        await using HubClient watcher = await HubClient.ConnectAsync(server);
        await watcher.JoinAsync(project.Value);

        using HttpResponseMessage response = await ImportAsync(server, project, track, File());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        MidiImportResult result = (await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.MidiImportResult))!;
        ProjectChange heard = await watcher.NextChangeAsync();
        Assert.Equal(("midi_clip_import", result.Revision), (heard.Reason, heard.Revision));
        ProjectDocument announced = ProjectJson.Read(heard.Document);
        Assert.Equal(result.Clip, Assert.Single(announced.Tracks.Single().MidiClips).Id.Value);
        Assert.Equal("midi_clip_import", (await HistoryAsync(server, _local, project)).UndoLabel);
    }
}
