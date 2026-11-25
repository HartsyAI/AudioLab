using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Store;
using AudioLab.Server.Transcription;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Server.Tests;

/// <summary>Builds the transcription jobs over a test store, with the fakes of the model and the window source.</summary>
internal static class TranscriptionTestSupport
{
    public static readonly CancellationToken None = CancellationToken.None;

    public static TranscriptionOptions Options { get; } = new() { RetryDelay = TimeSpan.Zero };

    public static byte[] Wav() => TestWav.Build(SampleEncoding.Pcm16, 1, 16000, 16000);

    public static TranscriptionStore Files(TestStore store) => new(store.Root, new AtomicFile(), NullLogger<TranscriptionStore>.Instance);

    /// <summary>The registry of the given models, over the catalog's entries; a model that is not in the catalog is still found by its id.</summary>
    public static ISpeechModels Registry(params ISpeechModel[] models)
    {
        return new SpeechModels(SpeechModelCatalog.Entries, models);
    }

    public static TranscriptionJobs Jobs(TestStore store, TranscriptionStore files, IAudioWindowSource source, ISpeechModel model)
    {
        return Jobs(store, files, source, Registry(model));
    }

    public static TranscriptionJobs Jobs(TestStore store, TranscriptionStore files, IAudioWindowSource source, ISpeechModels models)
    {
        return new TranscriptionJobs(files, store.Media, source, models, Options, store.Time, NullLogger<TranscriptionJobs>.Instance);
    }

    public static TranscriptionService Service(TestStore store, TranscriptionJobs jobs, TranscriptionStore files, ISpeechModels? models = null)
    {
        return new TranscriptionService(store.MediaFiles, jobs, files, models ?? Registry());
    }

    /// <summary>Runs turns until none is left: each turn is one chunk, so the count of model calls is the count of windows.</summary>
    public static async Task RunToEndAsync(TranscriptionJobs jobs)
    {
        while (await jobs.StepAsync(None))
        {
        }
    }

    public static async Task<TranscriptionStatus> StatusOfAsync(TranscriptionJobs jobs, UserId user, MediaId id)
    {
        return (await jobs.StatusAsync(user, id, None))!;
    }
}
