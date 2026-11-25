using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;
using Microsoft.Extensions.Logging.Abstractions;
using static AudioLab.Server.Tests.TranscriptionTestSupport;

namespace AudioLab.Server.Tests;

/// <summary>The contracts the job system keeps when things go wrong: the model a job keeps (D98), a state file that cannot be written, the
/// stop (D93), the transcript's size (D95), and a restart.</summary>
public sealed class TranscriptionContractTests
{
    private const string ModelA = "model-a";
    private const string ModelB = "model-b";

    private static readonly SpeechModelEntry _entryA = new(ModelA, SpeechModelCatalog.SpeechCapability, "a", "en");
    private static readonly SpeechModelEntry _entryB = new(ModelB, SpeechModelCatalog.SpeechCapability, "b", "en");

    [Fact]
    public async Task AUserChoiceReachesNewJobsAndAChangeDoesNotReachARunningOne()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        ISpeechModels models = new SpeechModels([_entryA, _entryB], [Named(ModelA, "from a"), Named(ModelB, "from b")]);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), models);
        TranscriptionService service = Service(store, jobs, files, models);
        MediaRecord running = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord later = await store.UploadAsync(Users.Alice, Wav());

        await service.ChooseSpeechModelAsync(Users.Alice, ModelA, None);
        await jobs.StartAsync(Users.Alice, running, None);
        Assert.True(await jobs.StepAsync(None));
        await service.ChooseSpeechModelAsync(Users.Alice, ModelB, None);
        await RunToEndAsync(jobs);

        TranscriptDocument first = (await files.ReadTranscriptAsync(Users.Alice, running.Id, None))!;
        Assert.Equal(ModelA, first.Model);
        Assert.All(first.Segments, segment => Assert.StartsWith("from a", segment.Text, StringComparison.Ordinal));

        await jobs.StartAsync(Users.Alice, later, None);
        await RunToEndAsync(jobs);
        Assert.Equal(ModelB, (await files.ReadTranscriptAsync(Users.Alice, later.Id, None))!.Model);
    }

    [Fact]
    public async Task AChoiceThatIsNotInstalledIsRefusedAndTheChoiceStaysAsItWas()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        ISpeechModels models = new SpeechModels([_entryA], [Named(ModelA, "from a")]);
        TranscriptionService service = Service(store, Jobs(store, files, new FakeWindowSource(100), models), files, models);

        await Assert.ThrowsAsync<SpeechModelChoiceException>(() => service.ChooseSpeechModelAsync(Users.Alice, ModelB, None));
        Assert.Equal(SpeechModelCatalog.DefaultSpeechModel, (await service.SpeechModelAsync(Users.Alice, None)).Model);
    }

    [Fact]
    public async Task AJobWhoseModelIsGoneFailsWithAFixedReasonAndKeepsItsCheckpoints()
    {
        using TempFolder folder = new();
        TestStore first = TestStore.On(folder.Path);
        TranscriptionStore firstFiles = Files(first);
        await firstFiles.WriteSpeechModelAsync(Users.Alice, ModelA, None);
        TranscriptionJobs firstJobs = Jobs(first, firstFiles, new FakeWindowSource(100), Named(ModelA, "from a"));
        MediaRecord media = await first.UploadAsync(Users.Alice, Wav());
        await firstJobs.StartAsync(Users.Alice, media, None);
        Assert.True(await firstJobs.StepAsync(None));
        first.Crash();

        await using TestStore second = TestStore.On(folder.Path);
        TranscriptionStore secondFiles = Files(second);
        TranscriptionJobs secondJobs = Jobs(second, secondFiles, new FakeWindowSource(100), Named(ModelB, "from b"));
        await RunToEndAsync(secondJobs);

        TranscriptionStatus status = await StatusOfAsync(secondJobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Equal("The speech model is not installed.", status.Reason);
        Assert.Single(await secondFiles.ReadChunksAsync(Users.Alice, media.Id, None));
    }

    [Fact]
    public async Task APauseKeepsEveryCompletedChunkAndAResumeRepeatsNone()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new();
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        Assert.True(await jobs.StepAsync(None));
        Assert.True(await jobs.StepAsync(None));

        await jobs.PauseRunningAsync(None);
        Assert.Equal(TranscriptionState.Paused, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        await jobs.ResumeAsync(Users.Alice, media.Id, None);
        await RunToEndAsync(jobs);

        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        Assert.Equal(4, model.Calls);
        Assert.Equal(4, model.Starts.Distinct().Count());
        Assert.Equal(4, (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Segments.Count);
    }

    [Fact]
    public async Task AStopAbandonsAChunkPastTheModelTimeoutAndPausesTheJob()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new();
        model.BlockAt(1);
        TranscriptionOptions options = new() { ModelTimeout = TimeSpan.FromMinutes(1), Retries = 0 };
        TranscriptionJobs jobs = new(
            files, store.Media, new FakeWindowSource(100), Registry(model), options, store.Time, NullLogger<TranscriptionJobs>.Instance);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        TranscriptionWorker worker = new(jobs, options, store.Time, NullLogger<TranscriptionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await model.Entered.WaitAsync(TimeSpan.FromSeconds(30));
        Task stop = worker.StopAsync(CancellationToken.None);
        store.Time.Advance(options.ModelTimeout);
        await stop.WaitAsync(TimeSpan.FromSeconds(30));

        TranscriptionStatus paused = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Paused, paused.State);
        Assert.Equal(1, paused.ChunksDone);
        Assert.Null(paused.Reason);

        await jobs.ResumeAsync(Users.Alice, media.Id, None);
        await RunToEndAsync(jobs);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        Assert.Equal(4, (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Segments.Count);
    }

    [Fact]
    public async Task AWorkerFailsATimedOutJobAndStillCompletesTheOthers()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionOptions options = new() { ModelTimeout = TimeSpan.FromMinutes(1), Retries = 0, RetryDelay = TimeSpan.FromMilliseconds(1) };
        HangingSpeechModel hanging = new();
        FakeSpeechModel answering = Named("fake-model", "ok");
        TranscriptionJobs jobs = new(
            files, store.Media, new FakeWindowSource(100), Registry(hanging, answering), options, store.Time, NullLogger<TranscriptionJobs>.Instance);
        await files.WriteSpeechModelAsync(Users.Alice, hanging.ModelId, None);
        await files.WriteSpeechModelAsync(Users.Bob, "fake-model", None);
        MediaRecord stuck = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord fine = await store.UploadAsync(Users.Bob, Wav());
        await jobs.StartAsync(Users.Alice, stuck, None);
        await jobs.StartAsync(Users.Bob, fine, None);
        TranscriptionWorker worker = new(jobs, options, store.Time, NullLogger<TranscriptionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        for (int tick = 0; tick < 10_000; tick++)
        {
            TranscriptionStatus failed = await StatusOfAsync(jobs, Users.Alice, stuck.Id);
            TranscriptionStatus done = await StatusOfAsync(jobs, Users.Bob, fine.Id);
            if (failed.State == TranscriptionState.Failed && done.State == TranscriptionState.Done)
            {
                break;
            }
            store.Time.Advance(options.ModelTimeout);
            await Task.Delay(2);
        }
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(TranscriptionState.Failed, (await StatusOfAsync(jobs, Users.Alice, stuck.Id)).State);
        Assert.Equal("The speech model did not answer in time.", (await StatusOfAsync(jobs, Users.Alice, stuck.Id)).Reason);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Bob, fine.Id)).State);
    }

    [Fact]
    public async Task AGracefulStopPausesAndAResumeRepeatsNoChunk()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new();
        model.BlockAt(2);
        TranscriptionOptions options = new() { ModelTimeout = TimeSpan.FromMinutes(1), RetryDelay = TimeSpan.FromMilliseconds(1) };
        TranscriptionJobs jobs = new(
            files, store.Media, new FakeWindowSource(100), Registry(model), options, store.Time, NullLogger<TranscriptionJobs>.Instance);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        TranscriptionWorker worker = new(jobs, options, store.Time, NullLogger<TranscriptionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await model.Entered.WaitAsync(TimeSpan.FromSeconds(30));
        // The stop comes while chunk 2 is running, and the chunk finishes inside the model timeout: it is kept, not abandoned.
        Task stop = worker.StopAsync(CancellationToken.None);
        model.Release();
        await stop.WaitAsync(TimeSpan.FromSeconds(30));

        TranscriptionStatus paused = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Paused, paused.State);
        Assert.Equal(3, paused.ChunksDone);

        await jobs.ResumeAsync(Users.Alice, media.Id, None);
        await RunToEndAsync(jobs);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        Assert.Equal(4, model.Calls);
        Assert.Equal(4, model.Starts.Distinct().Count());
    }

    [Fact]
    public async Task ANoRetryStartsWhenTheStopBeginsDuringTheWaitBeforeIt()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new() { Fail = call => call == 0 ? new SpeechModelException("the GPU is busy", retryable: true) : null };
        TranscriptionOptions options = new() { RetryDelay = TimeSpan.FromSeconds(10), Retries = 2 };
        TranscriptionJobs jobs = new(
            files, store.Media, new FakeWindowSource(100), Registry(model), options, store.Time, NullLogger<TranscriptionJobs>.Instance);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        Task<bool> step = jobs.StepAsync(None);
        for (int tick = 0; store.Time.NextTimerDelay is null; tick++)
        {
            Assert.True(tick < 10_000, "The retry's wait never started.");
            await Task.Delay(1);
        }
        jobs.BeginStop();
        store.Time.Advance(options.RetryDelay);
        await step.WaitAsync(TimeSpan.FromSeconds(30));
        await jobs.PauseRunningAsync(CancellationToken.None);

        Assert.Equal(1, model.Calls);
        Assert.Equal(TranscriptionState.Paused, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
    }

    [Fact]
    public async Task AStateThatCannotBeWrittenFailsItsJobWithTheReasonAndTheUsersNextJobStillRuns()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        MediaRecord broken = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord next = await store.UploadAsync(Users.Alice, Wav());
        string stateFile = files.Paths.TranscriptJobFile(Users.Alice, broken.Id);
        FakeSpeechModel model = new() { Fail = call => call == 0 ? BlockTheStateFile(stateFile) : null };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), model);
        await jobs.StartAsync(Users.Alice, broken, None);
        await jobs.StartAsync(Users.Alice, next, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus failed = await StatusOfAsync(jobs, Users.Alice, broken.Id);
        Assert.Equal(TranscriptionState.Failed, failed.State);
        Assert.Equal("The job state could not be saved; resume to try again.", failed.Reason);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, next.Id)).State);
    }

    [Fact]
    public async Task AFailedWriteToRunningFailsOnlyThatJobAndTheOthersStillRun()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        MediaRecord broken = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord other = await store.UploadAsync(Users.Bob, Wav());
        FakeSpeechModel model = new();
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), model);
        await jobs.StartAsync(Users.Alice, broken, None);
        await jobs.StartAsync(Users.Bob, other, None);
        BlockTheStateFile(files.Paths.TranscriptJobFile(Users.Alice, broken.Id));

        await RunToEndAsync(jobs);

        TranscriptionStatus failed = await StatusOfAsync(jobs, Users.Alice, broken.Id);
        Assert.Equal(TranscriptionState.Failed, failed.State);
        Assert.Equal("The job state could not be saved; resume to try again.", failed.Reason);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Bob, other.Id)).State);
    }

    [Fact]
    public async Task AFailedDoneWriteKeepsTheCheckpointsAndARestartFinishesWithoutAModelCall()
    {
        using TempFolder folder = new();
        TestStore first = TestStore.On(folder.Path);
        TranscriptionStore firstFiles = Files(first);
        FakeSpeechModel model = new();
        TranscriptionJobs jobs = Jobs(first, firstFiles, new FakeWindowSource(100), model);
        MediaRecord media = await first.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        for (int chunk = 0; chunk < 4; chunk++)
        {
            Assert.True(await jobs.StepAsync(None));
        }
        string stateFile = firstFiles.Paths.TranscriptJobFile(Users.Alice, media.Id);
        byte[] saved = await File.ReadAllBytesAsync(stateFile);
        BlockTheStateFile(stateFile);

        Assert.True(await jobs.StepAsync(None));

        TranscriptionStatus failed = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, failed.State);
        Assert.Equal("The job state could not be saved; resume to try again.", failed.Reason);
        Assert.Equal(4, (await firstFiles.ReadChunksAsync(Users.Alice, media.Id, None)).Count);
        Assert.NotNull(await firstFiles.ReadTranscriptAsync(Users.Alice, media.Id, None));
        Directory.Delete(stateFile);
        await File.WriteAllBytesAsync(stateFile, saved);
        first.Crash();

        await using TestStore second = TestStore.On(folder.Path);
        TranscriptionJobs secondJobs = Jobs(second, Files(second), new FakeWindowSource(100), model);
        int calls = model.Calls;
        await RunToEndAsync(secondJobs);

        Assert.Equal(calls, model.Calls);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(secondJobs, Users.Alice, media.Id)).State);
    }

    [Fact]
    public async Task AChangedChoiceDoesNotReachARunningJobAfterARestart()
    {
        using TempFolder folder = new();
        TestStore first = TestStore.On(folder.Path);
        TranscriptionStore firstFiles = Files(first);
        await firstFiles.WriteSpeechModelAsync(Users.Alice, ModelA, None);
        ISpeechModels models = Registry(Named(ModelA, "from a"), Named(ModelB, "from b"));
        TranscriptionJobs firstJobs = Jobs(first, firstFiles, new FakeWindowSource(100), models);
        MediaRecord media = await first.UploadAsync(Users.Alice, Wav());
        await firstJobs.StartAsync(Users.Alice, media, None);
        Assert.True(await firstJobs.StepAsync(None));
        Assert.True(await firstJobs.StepAsync(None));
        first.Crash();

        await using TestStore second = TestStore.On(folder.Path);
        TranscriptionStore secondFiles = Files(second);
        await secondFiles.WriteSpeechModelAsync(Users.Alice, ModelB, None);
        TranscriptionJobs secondJobs = Jobs(second, secondFiles, new FakeWindowSource(100), models);
        await RunToEndAsync(secondJobs);

        TranscriptDocument document = (await secondFiles.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        Assert.Equal(ModelA, document.Model);
        Assert.All(document.Segments, segment => Assert.StartsWith("from a", segment.Text, StringComparison.Ordinal));
        Assert.Equal(4, document.Segments.Count);
    }

    [Fact]
    public async Task AFailedJobResumesUnderItsOwnModelWhenTheChoiceHasChanged()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        await files.WriteSpeechModelAsync(Users.Alice, ModelA, None);
        FakeSpeechModel a = Named(ModelA, "from a");
        a.Fail = call => call == 1 ? new SpeechModelException("The model rejected the audio.", retryable: false) : null;
        ISpeechModels models = Registry(a, Named(ModelB, "from b"));
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), models);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);
        Assert.Equal(TranscriptionState.Failed, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);

        await files.WriteSpeechModelAsync(Users.Alice, ModelB, None);
        await jobs.ResumeAsync(Users.Alice, media.Id, None);
        await RunToEndAsync(jobs);

        TranscriptDocument document = (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        Assert.Equal(ModelA, document.Model);
        Assert.All(document.Segments, segment => Assert.StartsWith("from a", segment.Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AStartUnderAnotherModelMakesANewJobAfterTheFirstModelIsGone()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        await files.WriteSpeechModelAsync(Users.Alice, ModelA, None);
        ISpeechModels onlyB = Registry(Named(ModelB, "from b"));
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), onlyB);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);
        TranscriptionStatus gone = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, gone.State);
        Assert.Equal("The speech model is not installed.", gone.Reason);

        await files.WriteSpeechModelAsync(Users.Alice, ModelB, None);
        StartOutcome outcome = await jobs.StartAsync(Users.Alice, media, None);
        Assert.True(outcome.Created);
        await RunToEndAsync(jobs);

        Assert.Equal(ModelB, (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Model);
    }

    [Fact]
    public async Task ADoneJobUnderANewChoiceIsANewJob()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        await files.WriteSpeechModelAsync(Users.Alice, ModelA, None);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(28), Registry(Named(ModelA, "from a"), Named(ModelB, "from b")));
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);

        await files.WriteSpeechModelAsync(Users.Alice, ModelB, None);
        StartOutcome outcome = await jobs.StartAsync(Users.Alice, media, None);

        Assert.True(outcome.Created);
        await RunToEndAsync(jobs);
        Assert.Equal(ModelB, (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Model);
    }

    [Fact]
    public async Task AStaleChunkLogIsNeverReadByANewJob()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await files.AppendChunkAsync(Users.Alice, media.Id, new ChunkRecord(0, [new TranscriptSegment(0, 1, "stale", SpeechKind.Speech)]), None);
        FakeSpeechModel model = new();
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), model);

        await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);

        Assert.Equal(4, model.Calls);
        Assert.DoesNotContain(
            (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Segments,
            segment => segment.Text == "stale");
    }

    [Fact]
    public async Task AnAnswerLongerThanTheLimitsIsCutAndTheJobCompletes()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        string longText = new('x', TranscriptionLimits.MaxTextChars + 500);
        string longWord = new('w', TranscriptionLimits.MaxWordChars + 50);
        FakeSpeechModel model = new()
        {
            Answer = (call, window) =>
                [new SpeechSegment(1, 2, longText, SpeechKind.Speech, [new LyricWord(longWord, 1, 1.5)])],
        };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(28), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        TranscriptSegment segment = (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Segments[0];
        Assert.Equal(TranscriptionLimits.MaxTextChars, segment.Text.Length);
        Assert.Equal(TranscriptionLimits.MaxWordChars, Assert.Single(segment.Words!).Word.Length);
    }

    [Fact]
    public void ARegistryEntryWithAnEmptyOrLongIdStopsTheStart()
    {
        Assert.Throws<ArgumentException>(() => SpeechModelCatalog.Validate(
            [new SpeechModelEntry(" ", SpeechModelCatalog.SpeechCapability, "f", "en")]));
        Assert.Throws<ArgumentException>(() => SpeechModelCatalog.Validate(
            [new SpeechModelEntry(new string('m', TranscriptionLimits.MaxModelIdChars + 1), SpeechModelCatalog.SpeechCapability, "f", "en")]));
        SpeechModelCatalog.Validate([new SpeechModelEntry("ok", SpeechModelCatalog.SpeechCapability, "f", "en")]);
    }

    [Fact]
    public async Task AModelThatNeverAnswersFailsOnlyItsOwnJob()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionOptions options = new()
        {
            ModelTimeout = TimeSpan.FromMilliseconds(100),
            Retries = 0,
            RetryDelay = TimeSpan.FromMilliseconds(1),
        };
        HangingSpeechModel hanging = new();
        FakeSpeechModel answering = Named("fake-model", "ok");
        TranscriptionJobs jobs = new(
            files,
            store.Media,
            new FakeWindowSource(100),
            Registry(hanging, answering),
            options,
            TimeProvider.System,
            NullLogger<TranscriptionJobs>.Instance);
        await files.WriteSpeechModelAsync(Users.Alice, hanging.ModelId, None);
        await files.WriteSpeechModelAsync(Users.Bob, "fake-model", None);
        MediaRecord stuck = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord fine = await store.UploadAsync(Users.Bob, Wav());
        await jobs.StartAsync(Users.Alice, stuck, None);
        await jobs.StartAsync(Users.Bob, fine, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus failed = await StatusOfAsync(jobs, Users.Alice, stuck.Id);
        Assert.Equal(TranscriptionState.Failed, failed.State);
        Assert.Equal("The speech model did not answer in time.", failed.Reason);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Bob, fine.Id)).State);
    }

    [Fact]
    public async Task ATranscriptThatWouldPassItsLimitFailsAtTheChunkThatWouldPassIt()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        string text = new('x', TranscriptionLimits.MaxTextChars);
        FakeSpeechModel model = new()
        {
            Answer = (call, window) => Enumerable.Range(0, TranscriptionLimits.MaxSegmentsPerWindow)
                .Select(index => new SpeechSegment(index * 0.02, (index * 0.02) + 0.01, text, SpeechKind.Speech))
                .ToList(),
        };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100 * 28), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus status = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Contains("64 MiB", status.Reason, StringComparison.Ordinal);
        Assert.Contains("no transcript was kept", status.Reason, StringComparison.Ordinal);
        Assert.True(status.ChunksDone < 100, status.ChunksDone.ToString());
        Assert.Null(await files.ReadTranscriptAsync(Users.Alice, media.Id, None));
    }

    [Fact]
    public async Task AWordOfOnlySpacesIsDroppedAndTheJobStillCompletes()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new()
        {
            Answer = (call, window) =>
                [new SpeechSegment(1, 2, "hello", SpeechKind.Speech, [new LyricWord("   ", 1, 1.5), new LyricWord("hello", 1, 1.5)])],
        };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(28), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
    }

    [Fact]
    public async Task ADoneJobWhoseTranscriptBelongsToAnotherVersionStartsAgain()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(28), new FakeSpeechModel());
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);
        TranscriptDocument stored = (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        await files.WriteTranscriptAsync(Users.Alice, media.Id, stored with { Sha256 = new string('0', 64) }, None);

        StartOutcome outcome = await jobs.StartAsync(Users.Alice, media, None);

        Assert.True(outcome.Created);
    }

    [Fact]
    public async Task AFailedJobShowsItsProgressAfterARestartAndAnOlderVersionIsNotLoaded()
    {
        using TempFolder folder = new();
        TestStore first = TestStore.On(folder.Path);
        FakeSpeechModel model = new() { Fail = call => call == 2 ? new InvalidOperationException("The model has a bug.") : null };
        TranscriptionStore firstFiles = Files(first);
        TranscriptionJobs firstJobs = Jobs(first, firstFiles, new FakeWindowSource(100), model);
        MediaRecord media = await first.UploadAsync(Users.Alice, Wav());
        MediaRecord older = await first.UploadAsync(Users.Bob, Wav());
        await firstJobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(firstJobs);
        await firstJobs.StartAsync(Users.Bob, older, None);
        TranscriptionJobFile olderFile = firstFiles.ReadAllStates().Single(state => state.File.Owner == Users.Bob.Value).File;
        await firstFiles.WriteStateAsync(Users.Bob, older.Id, olderFile with { Version = 0 }, None);
        first.Crash();

        await using TestStore second = TestStore.On(folder.Path);
        TranscriptionStore secondFiles = Files(second);
        TranscriptionJobs secondJobs = Jobs(second, secondFiles, new FakeWindowSource(100), model);
        await secondJobs.WhenLoadedAsync();

        TranscriptionStatus failed = await StatusOfAsync(secondJobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, failed.State);
        Assert.Equal(2, failed.ChunksDone);
        Assert.Null(await secondJobs.StatusAsync(Users.Bob, older.Id, None));
    }

    private static FakeSpeechModel Named(string modelId, string text)
    {
        return new FakeSpeechModel
        {
            ModelId = modelId,
            Answer = (call, window) =>
                [new SpeechSegment(1, 2, text + " " + call.ToString(System.Globalization.CultureInfo.InvariantCulture), SpeechKind.Speech)],
        };
    }

    private static Exception BlockTheStateFile(string stateFile)
    {
        File.Delete(stateFile);
        Directory.CreateDirectory(stateFile);
        return new InvalidOperationException("The model has a bug.");
    }
}
