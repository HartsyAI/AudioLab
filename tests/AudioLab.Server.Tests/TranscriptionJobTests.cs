using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Media;
using AudioLab.Server.Transcription;
using Microsoft.Extensions.Logging.Abstractions;
using static AudioLab.Server.Tests.TranscriptionTestSupport;

namespace AudioLab.Server.Tests;

public sealed class TranscriptionJobTests
{
    // A file of ten hours: 1,286 windows of 30 s, each a counted model call. No clock moves; the count is the whole test.
    private const double TenHours = 36_000;
    private const int TenHourWindows = 1286;

    [Fact]
    public async Task TenHoursOfAudioRunsEveryWindowOnce()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new();
        FakeWindowSource source = new(TenHours);
        TranscriptionJobs jobs = Jobs(store, files, source, model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());

        StartOutcome started = await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);

        Assert.True(started.Created);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        Assert.Equal(TenHourWindows, model.Calls);
        Assert.Equal(TenHourWindows, source.Reads);
        TranscriptDocument document = (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        Assert.Equal(TenHourWindows, document.Segments.Count);
        Assert.Equal(TenHours, document.Duration);
        Assert.Equal(TranscriptionWindows.At(TenHourWindows - 1, TenHours).StartSeconds + 1, document.Segments[^1].Start);
        Assert.False(File.Exists(store.Paths.TranscriptChunkFile(Users.Alice, media.Id)));
    }

    [Fact]
    public async Task ACancelMidJobLeavesNoPartialTranscriptAndResumeFinishesIt()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new();
        model.BlockAt(5);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(TenHours), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        Task running = Task.Run(() => RunToEndAsync(jobs));
        await model.Entered.WaitAsync(TimeSpan.FromSeconds(30));
        TranscriptionStatus during = await jobs.CancelAsync(Users.Alice, media.Id, None);
        model.Release();
        await running;

        TranscriptionStatus cancelled = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.True(during.CancelRequested);
        Assert.Equal(TranscriptionState.Cancelled, cancelled.State);
        Assert.Equal(6, cancelled.ChunksDone);
        Assert.Null(await files.ReadTranscriptAsync(Users.Alice, media.Id, None));
        Assert.False(File.Exists(store.Paths.TranscriptFile(Users.Alice, media.Id)));
        Assert.Equal(6, (await files.ReadChunksAsync(Users.Alice, media.Id, None)).Count);

        await jobs.ResumeAsync(Users.Alice, media.Id, None);
        await RunToEndAsync(jobs);

        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        Assert.Equal(TenHourWindows, model.Calls);
        Assert.Equal(TenHourWindows, (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Segments.Count);
    }

    [Fact]
    public async Task ARestartResumesFromTheLastCheckpointAndCallsNoWindowTwice()
    {
        using TempFolder folder = new();
        TestStore first = TestStore.On(folder.Path);
        FakeSpeechModel firstModel = new();
        TranscriptionJobs firstJobs = Jobs(first, Files(first), new FakeWindowSource(TenHours), firstModel);
        MediaRecord media = await first.UploadAsync(Users.Alice, Wav());
        await firstJobs.StartAsync(Users.Alice, media, None);
        for (int turn = 0; turn < 10; turn++)
        {
            Assert.True(await firstJobs.StepAsync(None));
        }
        Assert.Equal(10, firstModel.Calls);
        first.Crash();

        await using TestStore second = TestStore.On(folder.Path);
        TranscriptionStore secondFiles = Files(second);
        FakeSpeechModel secondModel = new();
        TranscriptionJobs secondJobs = Jobs(second, secondFiles, new FakeWindowSource(TenHours), secondModel);
        await RunToEndAsync(secondJobs);

        Assert.Equal(TenHourWindows - 10, secondModel.Calls);
        Assert.Equal(TranscriptionWindows.At(10, TenHours).StartSeconds, secondModel.Starts[0]);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(secondJobs, Users.Alice, media.Id)).State);
        Assert.Equal(TenHourWindows, (await secondFiles.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Segments.Count);
    }

    [Fact]
    public async Task AnotherUsersMediaIsNotFoundToAnyJobRoute()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(10), new FakeSpeechModel());
        TranscriptionService service = Service(store, jobs, files);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await service.StartAsync(Users.Alice, media.Id, None);

        await Assert.ThrowsAsync<MediaNotFoundException>(() => service.StartAsync(Users.Bob, media.Id, None));
        await Assert.ThrowsAsync<MediaNotFoundException>(() => service.StatusAsync(Users.Bob, media.Id, None));
        await Assert.ThrowsAsync<MediaNotFoundException>(() => service.CancelAsync(Users.Bob, media.Id, None));
        await Assert.ThrowsAsync<MediaNotFoundException>(() => service.ResumeAsync(Users.Bob, media.Id, None));
        await Assert.ThrowsAsync<MediaNotFoundException>(() => service.TranscriptAsync(Users.Bob, media.Id, None));
        Assert.Equal(TranscriptionState.Queued, (await service.StatusAsync(Users.Alice, media.Id, None)).State);
    }

    [Fact]
    public async Task AMalformedAnswerFailsTheJobAndStoresNothingOfThatWindow()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new()
        {
            Answer = (call, window) => call == 2
                ? [new SpeechSegment(double.NaN, 1, "not a time", SpeechKind.Speech)]
                : [new SpeechSegment(1, 2, "fine", SpeechKind.Speech)],
        };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(TenHours), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus status = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Contains("malformed", status.Reason, StringComparison.Ordinal);
        Assert.Equal(2, status.ChunksDone);
        Assert.Equal(2, (await files.ReadChunksAsync(Users.Alice, media.Id, None)).Count);
        Assert.Null(await files.ReadTranscriptAsync(Users.Alice, media.Id, None));
    }

    [Fact]
    public async Task ARetryableErrorIsTriedTwiceMoreThenTheJobFails()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new() { Fail = call => call < 3 ? new SpeechModelException("the GPU is busy", retryable: true) : null };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(TenHours), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus status = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Equal("the GPU is busy", status.Reason);
        Assert.Equal(3, model.Calls);
    }

    [Fact]
    public async Task AModelThatIsNotInstalledFailsAtOnce()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(TenHours), Registry());
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus status = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Equal("The speech model is not installed.", status.Reason);
        Assert.Equal(0, status.ChunksDone);
    }

    [Fact]
    public async Task AStoredTranscriptThatIsNotSoundIsRefusedNotRepaired()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionService service = Service(store, Jobs(store, files, new FakeWindowSource(10), new FakeSpeechModel()), files);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        string path = store.Paths.TranscriptFile(Users.Alice, media.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string backwards = "{\"start\":0.5,\"end\":0.2,\"text\":\"backwards\",\"kind\":\"speech\"}";
        string header = "{\"version\":1,\"media\":\"" + media.Id.Value + "\",\"sha256\":\"" + media.Sha256 + "\",";
        File.WriteAllText(path, header + "\"model\":\"m\",\"duration\":1,\"segments\":[" + backwards + "]}");

        await Assert.ThrowsAsync<TranscriptRefusedException>(() => service.TranscriptAsync(Users.Alice, media.Id, None));
        File.WriteAllText(path, "not json at all");
        await Assert.ThrowsAsync<TranscriptRefusedException>(() => service.TranscriptAsync(Users.Alice, media.Id, None));
        Assert.Equal("not json at all", File.ReadAllText(path));
    }

    [Fact]
    public async Task AQueuedTranscriptionStopsTheMediaFromBeingDeleted()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(10), new FakeSpeechModel());
        MediaService guarded = new(
            store.Media,
            new MediaReferences(store.Open, store.Projects, NullLogger<MediaReferences>.Instance, null),
            store.Root,
            store.Ids,
            new MediaOptions(),
            store.Time,
            store.Rebuilder,
            new OptionsMediaQuota(new MediaOptions()),
            NullLogger<MediaService>.Instance,
            jobs);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await Assert.ThrowsAsync<MediaTranscriptionBusyException>(() => guarded.DeleteAsync(Users.Alice, media.Id, None));
        await jobs.CancelAsync(Users.Alice, media.Id, None);
        await guarded.DeleteAsync(Users.Alice, media.Id, None);

        Assert.Null(store.Media.Find(Users.Alice, media.Id));
        Assert.False(File.Exists(store.Paths.TranscriptJobFile(Users.Alice, media.Id)));
    }
}
