using System.Globalization;
using System.Text.RegularExpressions;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Server.Media;
using AudioLab.Server.Realtime;
using AudioLab.Server.Store;
using static AudioLab.Server.Tests.MediaTestSupport;

namespace AudioLab.Server.Tests;

public sealed partial class MediaCommandTests
{
    // Every command that names a media file, each with its own tests below. A command that is added and names one fails
    // CommandsThatNameMediaAreAllCoveredHere until it has a test here and a place in this list.
    private static readonly string[] _covered = ["beat_lane_add", "beat_lane_set_media", "clip_add", "instrument_set_sample"];

    private static readonly CancellationToken _none = CancellationToken.None;

    private static string Seconds(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static async Task<(TestStore Store, ProjectId Project, MediaRecord Own, MediaRecord Foreign)> StartAsync(TempFolder folder)
    {
        TestStore store = TestStore.On(folder.Path, registry: CoreCommands.CreateRegistry());
        MediaRecord own = await store.UploadAsync(Users.Alice, Wav(), "own.wav");
        MediaRecord foreign = await store.UploadAsync(Users.Bob, Wav(), "theirs.wav");
        ProjectId project = await NewProjectAsync(store, Users.Alice);
        return (store, project, own, foreign);
    }

    // The command must be refused with this code, and the document, its revision and its history must be exactly as they were.
    private static async Task<CommandError> RefusedAsync(TestStore store, ProjectId project, string name, string members, CommandErrorCode code)
    {
        ProjectSession session = await store.Open.OpenAsync(Users.Alice, project, _none);
        ProjectSnapshot before = session.Snapshot;
        GestureId gesture = GestureId.From("drag");

        string json = $"{{\"project\":\"{project.Value}\",{members}}}";
        CommandReply reply = await store.Edits.ApplyAsync(Users.Alice, project, Client, name, json, gesture, _none);

        Assert.False(reply.Succeeded);
        Assert.False(reply.Changed);
        Assert.Empty(reply.Created);
        HubCommandError error = Assert.Single(reply.Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal(before, session.Snapshot);
        Assert.Null(session.OpenGestureOf(Client));
        return new CommandError(error.Code, error.Message);
    }

    private static async Task<CommandReply> OkAsync(TestStore store, ProjectId project, string name, string members)
    {
        CommandReply reply = await ApplyAsync(store, Users.Alice, project, name, members);
        Assert.True(reply.Succeeded, string.Join("; ", reply.Errors.Select(error => error.Message)));
        return reply;
    }

    private static async Task<ProjectDocument> DocumentAsync(TestStore store, ProjectId project)
    {
        return (await store.Open.OpenAsync(Users.Alice, project, _none)).Snapshot.Document;
    }

    [Fact]
    public async Task ClipAddIsRefusedForMissingAndForeignMediaAndForAWrongDurationBeforeItApplies()
    {
        using TempFolder folder = new();
        (TestStore store, ProjectId project, MediaRecord own, MediaRecord foreign) = await StartAsync(folder);
        await using TestStore scope = store;
        string track = await AddTrackAsync(store, Users.Alice, project);
        string Add(string media, double duration) => $"\"track\":\"{track}\",\"media\":\"{media}\",\"duration\":{Seconds(duration)}";

        CommandError missing = await RefusedAsync(store, project, "clip_add", Add("med_nothing", 0.5), CommandErrorCode.NotFound);
        CommandError theirs = await RefusedAsync(store, project, "clip_add", Add(foreign.Id.Value, foreign.Duration), CommandErrorCode.NotFound);
        Assert.Equal(missing, theirs);
        await RefusedAsync(store, project, "clip_add", Add(own.Id.Value, own.Duration + 0.002), CommandErrorCode.Rejected);
        await RefusedAsync(store, project, "clip_add", Add(own.Id.Value, own.Duration - 0.002), CommandErrorCode.Rejected);
        await RefusedAsync(store, project, "clip_add", Add(own.Id.Value, 600), CommandErrorCode.Rejected);

        CommandReply within = await OkAsync(store, project, "clip_add", Add(own.Id.Value, own.Duration + 0.0005));
        AudioClip clip = (await DocumentAsync(store, project)).Tracks.Single().Clips.Single();
        Assert.Equal(within.Created.Single(), clip.Id.Value);
        Assert.Equal(own.Duration + 0.0005, clip.Duration);
        Assert.Equal(own.Id, clip.Media);
    }

    [Fact]
    public async Task BeatLaneAddIsRefusedForMissingAndForeignMediaAndNeedsNoneWhenItNamesNone()
    {
        using TempFolder folder = new();
        (TestStore store, ProjectId project, MediaRecord own, MediaRecord foreign) = await StartAsync(folder);
        await using TestStore scope = store;

        CommandError missing = await RefusedAsync(store, project, "beat_lane_add", "\"media\":\"med_nothing\"", CommandErrorCode.NotFound);
        CommandError theirs = await RefusedAsync(store, project, "beat_lane_add", $"\"media\":\"{foreign.Id.Value}\"", CommandErrorCode.NotFound);
        Assert.Equal(missing, theirs);

        await OkAsync(store, project, "beat_lane_add", $"\"media\":\"{own.Id.Value}\"");
        await OkAsync(store, project, "beat_lane_add", "\"name\":\"No sample\"");
        BeatLane[] lanes = [.. (await DocumentAsync(store, project)).BeatPattern.Lanes];
        Assert.Equal([own.Id, (MediaId?)null], lanes.Select(lane => lane.Media));
    }

    [Fact]
    public async Task BeatLaneSetMediaIsRefusedForMissingAndForeignMediaAndMayClearTheLane()
    {
        using TempFolder folder = new();
        (TestStore store, ProjectId project, MediaRecord own, MediaRecord foreign) = await StartAsync(folder);
        await using TestStore scope = store;
        string lane = (await OkAsync(store, project, "beat_lane_add", string.Empty)).Created.Single();

        string Set(string media) => $"\"lane\":\"{lane}\",\"media\":\"{media}\"";

        CommandError missing = await RefusedAsync(store, project, "beat_lane_set_media", Set("med_nothing"), CommandErrorCode.NotFound);
        CommandError theirs = await RefusedAsync(store, project, "beat_lane_set_media", Set(foreign.Id.Value), CommandErrorCode.NotFound);
        Assert.Equal(missing, theirs);

        await OkAsync(store, project, "beat_lane_set_media", $"\"lane\":\"{lane}\",\"media\":\"{own.Id.Value}\"");
        Assert.Equal(own.Id, (await DocumentAsync(store, project)).BeatPattern.Lanes.Single().Media);
        await OkAsync(store, project, "beat_lane_set_media", $"\"lane\":\"{lane}\"");
        Assert.Null((await DocumentAsync(store, project)).BeatPattern.Lanes.Single().Media);
    }

    [Fact]
    public async Task InstrumentSetSampleIsRefusedForMissingAndForeignMediaAndMayClearTheSample()
    {
        using TempFolder folder = new();
        (TestStore store, ProjectId project, MediaRecord own, MediaRecord foreign) = await StartAsync(folder);
        await using TestStore scope = store;
        string track = (await OkAsync(store, project, "track_add", "\"kind\":\"instrument\",\"preset\":\"Sampler\"")).Created.Single();
        string Set(string media) => $"\"track\":\"{track}\",\"sample\":\"{media}\",\"sampleName\":\"snare\"";

        CommandError missing = await RefusedAsync(store, project, "instrument_set_sample", Set("med_nothing"), CommandErrorCode.NotFound);
        CommandError theirs = await RefusedAsync(store, project, "instrument_set_sample", Set(foreign.Id.Value), CommandErrorCode.NotFound);
        Assert.Equal(missing, theirs);

        await OkAsync(store, project, "instrument_set_sample", Set(own.Id.Value));
        SamplerInstrument sampler = (SamplerInstrument)(await DocumentAsync(store, project)).Tracks.Single().Instrument!;
        Assert.Equal(own.Id, sampler.Sample);
        await OkAsync(store, project, "instrument_set_sample", $"\"track\":\"{track}\"");
    }

    [Fact]
    public void CommandsThatNameMediaAreAllCoveredHere()
    {
        CommandRegistry registry = CoreCommands.CreateRegistry();
        Type[] carriers =
        [
            typeof(AudioClip), typeof(BeatLane), typeof(Instrument), typeof(SamplerInstrument), typeof(Track), typeof(ProjectDocument),
        ];
        static bool NamesMedia(Type type) => type == typeof(MediaId) || type == typeof(MediaId?);

        string[] naming =
        [
            .. registry.Commands
                .Where(descriptor => descriptor.CommandType.GetProperties().Any(property => NamesMedia(property.PropertyType)))
                .Select(descriptor => descriptor.Name)
                .Order(StringComparer.Ordinal),
        ];
        string[] embedding =
        [
            .. registry.Commands
                .Where(descriptor => descriptor.CommandType.GetProperties().Any(property => carriers.Contains(property.PropertyType)))
                .Select(descriptor => descriptor.Name),
        ];

        Assert.Equal(_covered, naming);
        Assert.Empty(embedding);
    }

    [Fact]
    public async Task MoveSplitAndDuplicateOfAClipAreNotRefusedBecauseTheirFileIsNamedAlready()
    {
        using TempFolder folder = new();
        (TestStore store, ProjectId project, MediaRecord own, _) = await StartAsync(folder);
        await using TestStore scope = store;
        string clip = await AddClipAsync(store, Users.Alice, project, own);

        await OkAsync(store, project, "clip_move", $"\"clip\":\"{clip}\",\"start\":4");
        await OkAsync(store, project, "clip_duplicate", $"\"clip\":\"{clip}\"");
        await OkAsync(store, project, "clip_split", $"\"clip\":\"{clip}\",\"at\":4.25");
        Assert.Equal(3, (await DocumentAsync(store, project)).Tracks.Single().Clips.Count);
    }

    [Fact]
    public async Task AFileBeingDeletedCannotBeNewlyNamedButADocumentThatNamesItStillWorks()
    {
        using TempFolder folder = new();
        (TestStore store, ProjectId project, MediaRecord own, _) = await StartAsync(folder);
        await using TestStore scope = store;
        MediaRecord spare = await store.UploadAsync(Users.Alice, Wav(), "spare.wav");
        string clip = await AddClipAsync(store, Users.Alice, project, own);
        string track = (await DocumentAsync(store, project)).Tracks.Single().Id.Value;
        string Add(MediaRecord media) => $"\"track\":\"{track}\",\"media\":\"{media.Id.Value}\",\"duration\":{Seconds(media.Duration)}";

        Assert.NotNull(store.Media.BeginDelete(Users.Alice, spare.Id));
        Assert.NotNull(store.Media.BeginDelete(Users.Alice, own.Id));
        await RefusedAsync(store, project, "clip_add", Add(spare), CommandErrorCode.NotFound);
        await OkAsync(store, project, "clip_duplicate", $"\"clip\":\"{clip}\"");
        await store.Open.CloseAsync(Users.Alice, project, _none);
        Assert.Equal(2, (await DocumentAsync(store, project)).Tracks.Single().Clips.Count);

        store.Media.CancelDelete(Users.Alice, spare.Id);
        await OkAsync(store, project, "clip_add", Add(spare));
    }

    [Fact]
    public async Task ADeleteAndAClipAddRacingForTheSameFileNeverLeaveAClipOfADeletedFile()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: CoreCommands.CreateRegistry());
        ProjectId project = await NewProjectAsync(store, Users.Alice);
        string track = await AddTrackAsync(store, Users.Alice, project);

        for (int round = 0; round < 40; round++)
        {
            MediaRecord media = await store.UploadAsync(Users.Alice, Wav(480), $"race-{round}.wav");
            string members = $"\"track\":\"{track}\",\"media\":\"{media.Id.Value}\",\"duration\":{Seconds(media.Duration)}";
            Task<CommandReply> add = Task.Run(() => ApplyAsync(store, Users.Alice, project, "clip_add", members));
            Task delete = Task.Run(() => store.MediaFiles.DeleteAsync(Users.Alice, media.Id, _none));
            try
            {
                await delete;
            }
            catch (MediaInUseException)
            {
                // The add won: its clip must be there.
            }
            CommandReply reply = await add;

            bool deleted = store.Media.Find(Users.Alice, media.Id) is null;
            bool referenced = ProjectContent.ReferencedMedia(await DocumentAsync(store, project)).Contains(media.Id);
            Assert.False(deleted && referenced, $"round {round}: the file is gone but a clip still plays it");
            string state = referenced ? "there" : "not there";
            Assert.True(reply.Succeeded == referenced, $"round {round}: the add said {reply.Succeeded} but the clip is {state}");
        }
    }

    [Fact]
    public async Task AProjectFileThatNamesMediaItsOwnerDoesNotHaveIsRefusedWhenOpenedAndWhenCopied()
    {
        using TempFolder folder = new();
        (TestStore store, ProjectId project, MediaRecord own, MediaRecord foreign) = await StartAsync(folder);
        await using TestStore scope = store;
        await AddClipAsync(store, Users.Alice, project, own);
        await store.Service.SaveAsync(Users.Alice, project, _none);
        await store.Open.CloseAsync(Users.Alice, project, _none);
        string file = store.Paths.ProjectFile(Users.Alice, project);
        string good = await File.ReadAllTextAsync(file);

        async Task<IReadOnlyList<string>> OpenWith(string text)
        {
            await File.WriteAllTextAsync(file, text);
            Task Open() => store.Service.OpenAsync(Users.Alice, project, _none);
            Task Copy() => store.Service.DuplicateAsync(Users.Alice, project, null, _none);
            ProjectFormatException error = await Assert.ThrowsAsync<ProjectFormatException>(Open);
            ProjectFormatException copy = await Assert.ThrowsAsync<ProjectFormatException>(Copy);
            Assert.Equal(error.Problems, copy.Problems);
            Assert.Null(store.Open.SessionIfOpen(Users.Alice, project));
            return error.Problems;
        }

        IReadOnlyList<string> missing = await OpenWith(good.Replace(own.Id.Value, "med_nothing", StringComparison.Ordinal));
        IReadOnlyList<string> theirs = await OpenWith(good.Replace(own.Id.Value, foreign.Id.Value, StringComparison.Ordinal));
        IReadOnlyList<string> wrongLength = await OpenWith(DurationPattern().Replace(good, "\"duration\":9.5"));
        Assert.Equal(missing, theirs);
        Assert.StartsWith("clip ", missing.Single(), StringComparison.Ordinal);
        Assert.DoesNotContain(foreign.Id.Value, string.Join(' ', theirs), StringComparison.Ordinal);
        Assert.Contains("9.5 s", wrongLength.Single(), StringComparison.Ordinal);

        await File.WriteAllTextAsync(file, good);
        Assert.NotNull(await store.Service.OpenAsync(Users.Alice, project, _none));
    }

    [Fact]
    public async Task AUserCanPlayTheirOwnFileInTheirOwnProjectWhileAnotherUsersProjectCannotNameIt()
    {
        using TempFolder folder = new();
        (TestStore store, ProjectId alices, MediaRecord own, _) = await StartAsync(folder);
        await using TestStore scope = store;
        ProjectId bobs = await NewProjectAsync(store, Users.Bob);
        string track = (await ApplyAsync(store, Users.Bob, bobs, "track_add", "\"kind\":\"audio\"")).Created.Single();

        string members = $"\"track\":\"{track}\",\"media\":\"{own.Id.Value}\",\"duration\":{Seconds(own.Duration)}";
        CommandReply reply = await ApplyAsync(store, Users.Bob, bobs, "clip_add", members);

        Assert.False(reply.Succeeded);
        Assert.Equal(CommandErrorCode.NotFound, reply.Errors.Single().Code);
        Assert.NotNull(await AddClipAsync(store, Users.Alice, alices, own));
    }

    [GeneratedRegex("\"duration\":[0-9.]+")]
    private static partial Regex DurationPattern();
}
