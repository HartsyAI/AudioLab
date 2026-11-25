using AudioLab.Audio.Tests.Support;
using AudioLab.Audio.Wav;
using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static AudioLab.Server.Tests.TranscriptionTestSupport;

namespace AudioLab.Server.Tests;

/// <summary>The job system when things go wrong: a torn write, a damaged file, a model that fails in ways it should not, a restart, and the
/// limits.</summary>
public sealed class TranscriptionRecoveryTests
{
    private const string GenericReason = "The transcription stopped on an unexpected error; resume to try again.";

    [Fact]
    public async Task ADiskFullMidChunkLeavesNoTornLineInTheTranscript()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        string chunks = store.Paths.TranscriptChunkFile(Users.Alice, media.Id);
        FakeSpeechModel model = new() { Fail = call => call == 2 ? DiskFullAfterATornLine(chunks) : null };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), model);
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus failed = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, failed.State);
        Assert.Equal(2, failed.ChunksDone);
        model.Fail = null;
        await jobs.ResumeAsync(Users.Alice, media.Id, None);
        await RunToEndAsync(jobs);

        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        TranscriptDocument document = (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        Assert.Equal(new double[] { 1, 29, 57, 85 }, document.Segments.Select(segment => segment.Start).ToArray());
    }

    [Fact]
    public async Task ATornLineLeftByACrashIsCutOffAndTheJobCompletes()
    {
        using TempFolder folder = new();
        TestStore first = TestStore.On(folder.Path);
        TranscriptionJobs firstJobs = Jobs(first, Files(first), new FakeWindowSource(100), new FakeSpeechModel());
        MediaRecord media = await first.UploadAsync(Users.Alice, Wav());
        await firstJobs.StartAsync(Users.Alice, media, None);
        Assert.True(await firstJobs.StepAsync(None));
        Assert.True(await firstJobs.StepAsync(None));
        File.AppendAllText(first.Paths.TranscriptChunkFile(Users.Alice, media.Id), "{\"chunk\":2,\"segments\":[{\"sta");
        first.Crash();

        await using TestStore second = TestStore.On(folder.Path);
        TranscriptionStore secondFiles = Files(second);
        TranscriptionJobs secondJobs = Jobs(second, secondFiles, new FakeWindowSource(100), new FakeSpeechModel());
        await RunToEndAsync(secondJobs);

        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(secondJobs, Users.Alice, media.Id)).State);
        TranscriptDocument document = (await secondFiles.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        Assert.Equal(new double[] { 1, 29, 57, 85 }, document.Segments.Select(segment => segment.Start).ToArray());
    }

    [Fact]
    public async Task AWindowThatCannotBeReadFailsTheJobWithTheReasonAndKeepsItsChunks()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        BrokenWindowSource source = new(100, new WavFormatException("The data chunk is damaged."));
        TranscriptionJobs jobs = Jobs(store, files, source, new FakeSpeechModel());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus status = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Equal("The media file could not be read; resume to try again.", status.Reason);
        Assert.Equal(0, status.ChunksDone);
    }

    [Fact]
    public async Task AnUnreadableStoredFileIsReportedAsUnreadableNotAsAFullStore()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        TranscriptionJobs jobs = Jobs(store, files, new BrokenWindowSource(100, new IOException("The file is locked.")), new FakeSpeechModel());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        Assert.Equal("The media file could not be read; resume to try again.", (await StatusOfAsync(jobs, Users.Alice, media.Id)).Reason);
    }

    [Fact]
    public async Task AModelThatThrowsSomethingOtherThanAModelErrorFailsTheJobAndTheLoopGoesOn()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new() { Fail = _ => new InvalidOperationException("The model has a bug.") };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord other = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        await jobs.StartAsync(Users.Alice, other, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus status = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Equal(GenericReason, status.Reason);
        Assert.Equal(TranscriptionState.Failed, (await StatusOfAsync(jobs, Users.Alice, other.Id)).State);
    }

    [Fact]
    public async Task AModelThatNeverAnswersFailsTheJobAfterItsRetriesAndTheLoopGoesOn()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        HangingSpeechModel model = new();
        TranscriptionOptions options = new() { RetryDelay = TimeSpan.Zero, ModelTimeout = TimeSpan.FromMinutes(1) };
        TranscriptionJobs jobs = new(
            files,
            store.Media,
            new FakeWindowSource(100),
            Registry(model),
            options,
            store.Time,
            NullLogger<TranscriptionJobs>.Instance);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        Task run = RunToEndAsync(jobs);
        for (int tick = 0; !run.IsCompleted; tick++)
        {
            Assert.True(tick < 10_000, "The job never finished.");
            store.Time.Advance(TimeSpan.FromMinutes(1));
            await Task.WhenAny(run, Task.Delay(TimeSpan.FromMilliseconds(2)));
        }
        await run;

        TranscriptionStatus status = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Equal("The speech model did not answer in time.", status.Reason);
        Assert.Equal(3, model.Calls);
    }

    [Fact]
    public async Task ASpaceOrNewlineInTheTextIsNormalisedNotRefused()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new() { Answer = (call, window) => [new SpeechSegment(1, 2, "  hello\n  world ", SpeechKind.Speech)] };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(10), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        TranscriptDocument document = (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        Assert.Equal("hello world", document.Segments.Single().Text);
    }

    [Fact]
    public async Task AnAnswerOutOfOrderWithWordsOutsideTheirSegmentIsNormalisedAndStored()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        FakeSpeechModel model = new()
        {
            Answer = (call, window) =>
            [
                new SpeechSegment(5, 6, "second", SpeechKind.Speech),
                new SpeechSegment(1, 3, "first", SpeechKind.Speech, new ValueList<LyricWord>([new LyricWord("first", 0.5, 2.5)])),
            ],
        };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(10), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        TranscriptDocument document = (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        Assert.Equal(new[] { "first", "second" }, document.Segments.Select(segment => segment.Text).ToArray());
        LyricWord word = document.Segments[0].Words!.Single();
        Assert.Equal(1, word.Start);
        Assert.Equal(2.5, word.End);
    }

    [Fact]
    public async Task ATranscriptThatWouldPassTheSegmentLimitFailsAtTheChunkThatPassesIt()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        // 201 windows of 1,000 segments: the 200,000 that one transcript may hold are reached at window 199, the next one is refused.
        FakeSpeechModel model = new()
        {
            Answer = (call, window) => Enumerable.Range(0, 1000)
                .Select(index => new SpeechSegment(index * 0.02, (index * 0.02) + 0.01, "s", SpeechKind.Speech))
                .ToList(),
        };
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(201 * 28), model);
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);

        await RunToEndAsync(jobs);

        TranscriptionStatus status = await StatusOfAsync(jobs, Users.Alice, media.Id);
        Assert.Equal(TranscriptionState.Failed, status.State);
        Assert.Equal(200, status.ChunksDone);
        Assert.Contains("200000", status.Reason, StringComparison.Ordinal);
        Assert.Null(await files.ReadTranscriptAsync(Users.Alice, media.Id, None));
    }

    [Fact]
    public async Task AJobKeepsTheModelItStartedWithAfterARestart()
    {
        using TempFolder folder = new();
        TestStore first = TestStore.On(folder.Path);
        TranscriptionStore firstFiles = Files(first);
        await firstFiles.WriteSpeechModelAsync(Users.Alice, "model-a", None);
        TranscriptionJobs firstJobs = Jobs(first, firstFiles, new FakeWindowSource(100), Answering("from a", "model-a"));
        MediaRecord media = await first.UploadAsync(Users.Alice, Wav());
        await firstJobs.StartAsync(Users.Alice, media, None);
        Assert.True(await firstJobs.StepAsync(None));
        Assert.True(await firstJobs.StepAsync(None));
        first.Crash();

        TestStore second = TestStore.On(folder.Path);
        ISpeechModels both = Registry(Answering("from a", "model-a"), Answering("from b", "model-b"));
        TranscriptionJobs secondJobs = Jobs(second, Files(second), new FakeWindowSource(100), both);
        Assert.True(await secondJobs.StepAsync(None));
        second.Crash();

        await using TestStore third = TestStore.On(folder.Path);
        TranscriptionStore thirdFiles = Files(third);
        TranscriptionJobs thirdJobs = Jobs(third, thirdFiles, new FakeWindowSource(100), both);
        await RunToEndAsync(thirdJobs);

        TranscriptDocument document = (await thirdFiles.ReadTranscriptAsync(Users.Alice, media.Id, None))!;
        Assert.Equal("model-a", document.Model);
        Assert.All(document.Segments, segment => Assert.StartsWith("from a", segment.Text, StringComparison.Ordinal));
        Assert.Equal(4, document.Segments.Count);
    }

    [Fact]
    public async Task AFinishedJobReportsItsProgressAfterARestart()
    {
        using TempFolder folder = new();
        TestStore first = TestStore.On(folder.Path);
        TranscriptionJobs firstJobs = Jobs(first, Files(first), new FakeWindowSource(100), new FakeSpeechModel());
        MediaRecord media = await first.UploadAsync(Users.Alice, Wav());
        await firstJobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(firstJobs);
        first.Crash();

        await using TestStore second = TestStore.On(folder.Path);
        TranscriptionJobs secondJobs = Jobs(second, Files(second), new FakeWindowSource(100), new FakeSpeechModel());
        TranscriptionStatus status = await StatusOfAsync(secondJobs, Users.Alice, media.Id);

        Assert.Equal(TranscriptionState.Done, status.State);
        Assert.Equal(4, status.ChunksDone);
        Assert.Equal(100, status.ReachedSeconds);
    }

    [Fact]
    public async Task AMissingTranscriptIsMadeAgainByAStart()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), new FakeSpeechModel());
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);
        File.Delete(store.Paths.TranscriptFile(Users.Alice, media.Id));

        StartOutcome again = await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);

        Assert.True(again.Created);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Alice, media.Id)).State);
        Assert.Equal(4, (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Segments.Count);
    }

    [Fact]
    public async Task ARefusedTranscriptIsMadeAgainByAStart()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionJobs jobs = Jobs(store, files, new FakeWindowSource(100), new FakeSpeechModel());
        MediaRecord media = await store.UploadAsync(Users.Alice, Wav());
        await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);
        File.WriteAllText(store.Paths.TranscriptFile(Users.Alice, media.Id), "not json at all");

        StartOutcome again = await jobs.StartAsync(Users.Alice, media, None);
        await RunToEndAsync(jobs);

        Assert.True(again.Created);
        Assert.Equal(4, (await files.ReadTranscriptAsync(Users.Alice, media.Id, None))!.Segments.Count);
    }

    [Fact]
    public async Task TheLimitsOfTheUserAndOfTheServerHold()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        TranscriptionStore files = Files(store);
        TranscriptionOptions options = new() { MaxRunning = 2, MaxRunningPerUser = 1, MaxQueuedPerUser = 2, RetryDelay = TimeSpan.Zero };
        TranscriptionJobs jobs = new(
            files,
            store.Media,
            new FakeWindowSource(100),
            Registry(new FakeSpeechModel()),
            options,
            store.Time,
            NullLogger<TranscriptionJobs>.Instance);
        MediaRecord aliceOne = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord aliceTwo = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord aliceThree = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord bob = await store.UploadAsync(Users.Bob, Wav());
        MediaRecord carol = await store.UploadAsync(Users.Carol, Wav());
        await jobs.StartAsync(Users.Alice, aliceOne, None);
        await jobs.StartAsync(Users.Alice, aliceTwo, None);
        await jobs.StartAsync(Users.Bob, bob, None);
        await jobs.StartAsync(Users.Carol, carol, None);

        Assert.True(await jobs.StepAsync(None));

        Assert.Equal(TranscriptionState.Running, (await StatusOfAsync(jobs, Users.Alice, aliceOne.Id)).State);
        Assert.Equal(TranscriptionState.Queued, (await StatusOfAsync(jobs, Users.Alice, aliceTwo.Id)).State);
        Assert.Equal(TranscriptionState.Running, (await StatusOfAsync(jobs, Users.Bob, bob.Id)).State);
        Assert.Equal(TranscriptionState.Queued, (await StatusOfAsync(jobs, Users.Carol, carol.Id)).State);
        await Assert.ThrowsAsync<TranscriptionLimitException>(() => jobs.StartAsync(Users.Alice, aliceThree, None));
        await RunToEndAsync(jobs);
        Assert.Equal(TranscriptionState.Done, (await StatusOfAsync(jobs, Users.Carol, carol.Id)).State);
    }

    [Fact]
    public async Task AStoredWindowIsReadAsMonoAtSixteenKilohertz()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord media = await store.UploadAsync(Users.Alice, TestWav.Build(SampleEncoding.Pcm16, 2, 44100, 88200));
        StoredAudioWindowSource source = new(store.Root);
        TranscriptionWindow window = TranscriptionWindows.At(0, source.DurationOf(media));

        ReadOnlyMemory<float> samples = await source.ReadAsync(Users.Alice, media, window, None);

        Assert.InRange(samples.Length, 31_998, 32_002);
    }

    [Fact]
    public void ALimitThatCannotWorkStopsTheStartWhenTheServerStarts()
    {
        ServiceCollection services = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddTranscription(new TranscriptionOptions { MaxRunning = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddTranscription(new TranscriptionOptions { MaxRunningPerUser = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => services.AddTranscription(new TranscriptionOptions { RetryDelay = TimeSpan.FromSeconds(-1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddTranscription(new TranscriptionOptions { RetryDelay = TimeSpan.Zero }));
    }

    private static FakeSpeechModel Answering(string text, string? modelId = null)
    {
        return new FakeSpeechModel
        {
            ModelId = modelId ?? AudioLab.Core.Transcription.SpeechModelCatalog.DefaultSpeechModel,
            Answer = (call, window) =>
                [new SpeechSegment(1, 2, text + " " + call.ToString(System.Globalization.CultureInfo.InvariantCulture), SpeechKind.Speech)],
        };
    }

    private static Exception DiskFullAfterATornLine(string chunks)
    {
        File.AppendAllText(chunks, "{\"chunk\":2,\"segments\":[{\"start\":5");
        return new IOException("There is not enough space on the disk.", OperatingSystem.IsWindows() ? unchecked((int)0x80070070) : 28);
    }
}
