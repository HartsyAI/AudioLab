using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Server.Tests;

public sealed class HartsyInferenceSpeechModelTests
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(30);

    private static readonly SpeechModelEntry _custom = new("custom:model", SpeechModelCatalog.SpeechCapability, "custom-folder", "fr");

    private static SpeechWindow Window => new(0, new float[16_000]);

    [Fact]
    public async Task WithNoModelsFolderTheWindowFailsWithTheReasonAndNoRetry()
    {
        HartsyInferenceSpeechModel model = new(new TranscriptionOptions(), _custom, NullLogger<HartsyInferenceSpeechModel>.Instance);

        SpeechModelException error = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(Window, CancellationToken.None));

        Assert.Equal("The speech model is not installed.", error.Message);
        Assert.False(error.Retryable);
        Assert.False(HartsyInferenceSpeechModel.IsInstalled(new TranscriptionOptions(), _custom));
    }

    [Fact]
    public void AFolderMissingAnyRequiredFileIsNotInstalledAndNothingIsDownloaded()
    {
        using TempFolder folder = new();
        string stt = Path.Combine(folder.Path, "Audio", "stt", _custom.Folder);
        Directory.CreateDirectory(stt);
        File.WriteAllText(Path.Combine(stt, "model.safetensors"), "x");
        File.WriteAllText(Path.Combine(stt, "vocab.json"), "{}");
        File.WriteAllText(Path.Combine(stt, "merges.txt"), string.Empty);
        TranscriptionOptions options = new() { ModelsRoot = folder.Path };

        Assert.False(HartsyInferenceSpeechModel.IsInstalled(options, _custom));
        Assert.Equal(3, Directory.GetFiles(stt).Length);
    }

    [Fact]
    public async Task TheFolderAndLanguageComeFromTheEntryNotFromTheDefault()
    {
        using TempFolder folder = new();
        string stt = Path.Combine(folder.Path, "Audio", "stt", _custom.Folder);
        Directory.CreateDirectory(stt);
        foreach (string file in new[] { "model.safetensors", "vocab.json", "merges.txt", "config.json" })
        {
            File.WriteAllText(Path.Combine(stt, file), "x");
        }
        // The files are in the custom folder only; the default model's folder is empty.
        Assert.False(HartsyInferenceSpeechModel.IsInstalled(new TranscriptionOptions { ModelsRoot = folder.Path }, SpeechModelCatalog.Entries[0]));
        string? language = null;
        HartsyInferenceSpeechModel model = new(
            new TranscriptionOptions { ModelsRoot = folder.Path },
            _custom,
            NullLogger<HartsyInferenceSpeechModel>.Instance,
            native: (samples, request) =>
            {
                language = request.Language;
                return "  bonjour  ";
            });

        IReadOnlyList<SpeechSegment> answer = await model.TranscribeAsync(Window, CancellationToken.None);

        Assert.Equal("custom:model", model.ModelId);
        Assert.Equal("fr", language);
        Assert.Equal("bonjour", Assert.Single(answer).Text);
    }

    [Fact]
    public async Task ACallWaitsForACallTheCallerGaveUpOnAndNeverOverlapsIt()
    {
        using SemaphoreSlim entered = new(0);
        using ManualResetEventSlim release = new(false);
        object gate = new();
        int active = 0;
        int peak = 0;
        int calls = 0;
        using TempFolder folder = new();
        WriteInstalledFiles(folder.Path);
        HartsyInferenceSpeechModel model = new(
            new TranscriptionOptions { ModelsRoot = folder.Path },
            _custom,
            NullLogger<HartsyInferenceSpeechModel>.Instance,
            native: (samples, request) =>
            {
                lock (gate)
                {
                    calls++;
                    active++;
                    peak = Math.Max(peak, active);
                }
                entered.Release();
                release.Wait(_wait);
                lock (gate)
                {
                    active--;
                }
                return "hello";
            });
        using CancellationTokenSource giveUp = new();

        Task first = model.TranscribeAsync(Window, giveUp.Token);
        await entered.WaitAsync(_wait);
        giveUp.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        Task<IReadOnlyList<SpeechSegment>> second = model.TranscribeAsync(Window, CancellationToken.None);
        await Task.Delay(100);
        Assert.False(second.IsCompleted);
        lock (gate)
        {
            Assert.Equal(1, calls);
        }

        release.Set();
        IReadOnlyList<SpeechSegment> answer = await second.WaitAsync(_wait);

        Assert.Equal("hello", Assert.Single(answer).Text);
        lock (gate)
        {
            Assert.Equal(2, calls);
            Assert.Equal(1, peak);
        }
    }

    [Fact]
    public async Task ANativeFailureIsAFixedReasonWithTheDetailKeptAsTheCause()
    {
        using TempFolder folder = new();
        WriteInstalledFiles(folder.Path);
        HartsyInferenceSpeechModel model = new(
            new TranscriptionOptions { ModelsRoot = folder.Path },
            _custom,
            NullLogger<HartsyInferenceSpeechModel>.Instance,
            native: (samples, request) => throw new InvalidOperationException("C:\\models\\secret path"));

        SpeechModelException error = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(Window, CancellationToken.None));

        Assert.Equal("The speech model failed.", error.Message);
        Assert.False(error.Retryable);
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public async Task DisposeIsBoundedWhileACallStillRuns()
    {
        using TempFolder folder = new();
        WriteInstalledFiles(folder.Path);
        ManualResetEventSlim release = new(false);
        SemaphoreSlim entered = new(0);
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HartsyInferenceSpeechModel model = new(
            new TranscriptionOptions { ModelsRoot = folder.Path },
            _custom,
            NullLogger<HartsyInferenceSpeechModel>.Instance,
            native: (samples, request) =>
            {
                try
                {
                    entered.Release();
                    release.Wait(_wait);
                    return "late";
                }
                finally
                {
                    finished.TrySetResult();
                }
            });
        using CancellationTokenSource giveUp = new();
        Task first = model.TranscribeAsync(Window, giveUp.Token);
        await entered.WaitAsync(_wait);
        giveUp.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        await model.DisposeAsync().AsTask().WaitAsync(_wait);

        // The native call returns only after the test lets it; the events are disposed after it has finished with them.
        release.Set();
        await finished.Task.WaitAsync(_wait);
        release.Dispose();
        entered.Dispose();
    }

    [Fact]
    public async Task ACancelledOpenLeavesNothingOpenAndTheNextCallOpensAgain()
    {
        using TempFolder folder = new();
        WriteInstalledFiles(folder.Path);
        HartsyInferenceSpeechModel model = new(
            new TranscriptionOptions { ModelsRoot = folder.Path },
            _custom,
            NullLogger<HartsyInferenceSpeechModel>.Instance);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.TranscribeAsync(Window, cancelled.Token));

        // The placeholder files are not a model, so the open fails with its reason; the point is that the next call opens again.
        SpeechModelException error = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(Window, CancellationToken.None));
        Assert.Equal("The speech model could not be opened.", error.Message);
    }

    private static void WriteInstalledFiles(string root)
    {
        string stt = Path.Combine(root, "Audio", "stt", _custom.Folder);
        Directory.CreateDirectory(stt);
        foreach (string file in new[] { "model.safetensors", "vocab.json", "merges.txt", "config.json" })
        {
            File.WriteAllText(Path.Combine(stt, file), "x");
        }
    }
}
