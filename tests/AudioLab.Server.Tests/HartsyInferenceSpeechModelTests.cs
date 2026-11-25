using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Server.Tests;

public sealed class HartsyInferenceSpeechModelTests
{
    private static SpeechModelEntry Entry => SpeechModelCatalog.Entries[0];

    [Fact]
    public async Task WithNoModelsFolderTheWindowFailsWithTheReasonAndNoRetry()
    {
        HartsyInferenceSpeechModel model = new(new TranscriptionOptions(), Entry, NullLogger<HartsyInferenceSpeechModel>.Instance);

        SpeechModelException error = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(new SpeechWindow(0, new float[16_000]), CancellationToken.None));

        Assert.Equal("The speech model is not installed.", error.Message);
        Assert.False(error.Retryable);
    }

    [Fact]
    public async Task AFolderMissingAnyRequiredFileIsNotInstalledAndNothingIsDownloaded()
    {
        using TempFolder folder = new();
        string stt = Path.Combine(folder.Path, "Audio", "stt", Entry.Folder);
        Directory.CreateDirectory(stt);
        File.WriteAllText(Path.Combine(stt, "model.safetensors"), "x");
        File.WriteAllText(Path.Combine(stt, "vocab.json"), "{}");
        File.WriteAllText(Path.Combine(stt, "merges.txt"), string.Empty);
        HartsyInferenceSpeechModel model = new(
            new TranscriptionOptions { ModelsRoot = folder.Path },
            Entry,
            NullLogger<HartsyInferenceSpeechModel>.Instance);

        SpeechModelException error = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(new SpeechWindow(0, new float[16_000]), CancellationToken.None));

        Assert.Equal("The speech model is not installed.", error.Message);
        Assert.False(error.Retryable);
        Assert.Equal(3, Directory.GetFiles(stt).Length);
    }

    [Fact]
    public async Task ARetryNeverOverlapsACallTheCallerGaveUpOn()
    {
        using SemaphoreSlim entered = new(0);
        using ManualResetEventSlim release = new(false);
        object gate = new();
        int active = 0;
        int peak = 0;
        int calls = 0;
        HartsyInferenceSpeechModel model = new(
            new TranscriptionOptions(),
            Entry,
            NullLogger<HartsyInferenceSpeechModel>.Instance,
            native: samples =>
            {
                lock (gate)
                {
                    calls++;
                    active++;
                    peak = Math.Max(peak, active);
                }
                entered.Release();
                release.Wait();
                lock (gate)
                {
                    active--;
                }
                return "hello";
            });
        SpeechWindow window = new(0, new float[16_000]);
        using CancellationTokenSource giveUp = new();

        Task first = model.TranscribeAsync(window, giveUp.Token);
        await entered.WaitAsync(TimeSpan.FromSeconds(30));
        giveUp.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        SpeechModelException refused = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(window, CancellationToken.None));
        Assert.True(refused.Retryable);
        lock (gate)
        {
            Assert.Equal(1, calls);
        }

        release.Set();
        IReadOnlyList<SpeechSegment>? answer = null;
        for (int attempt = 0; answer is null && attempt < 500; attempt++)
        {
            try
            {
                answer = await model.TranscribeAsync(window, CancellationToken.None);
            }
            catch (SpeechModelException exception) when (exception.Retryable)
            {
                await Task.Delay(10);
            }
        }

        Assert.NotNull(answer);
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
        HartsyInferenceSpeechModel model = new(
            new TranscriptionOptions(),
            Entry,
            NullLogger<HartsyInferenceSpeechModel>.Instance,
            native: _ => throw new InvalidOperationException("C:\\models\\secret path"));

        SpeechModelException error = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(new SpeechWindow(0, new float[16_000]), CancellationToken.None));

        Assert.Equal("The speech model failed.", error.Message);
        Assert.False(error.Retryable);
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }
}
