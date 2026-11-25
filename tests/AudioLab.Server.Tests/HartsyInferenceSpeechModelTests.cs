using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;

namespace AudioLab.Server.Tests;

public sealed class HartsyInferenceSpeechModelTests
{
    [Fact]
    public async Task WithNoModelsFolderTheWindowFailsWithTheReasonAndNoRetry()
    {
        HartsyInferenceSpeechModel model = new(new TranscriptionOptions());

        SpeechModelException error = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(new SpeechWindow(0, new float[16_000]), CancellationToken.None));

        Assert.Equal("The speech model is not installed.", error.Message);
        Assert.False(error.Retryable);
    }

    [Fact]
    public async Task AFolderMissingAnyRequiredFileIsNotInstalledAndNothingIsDownloaded()
    {
        using TempFolder folder = new();
        string stt = Path.Combine(folder.Path, "Audio", "stt", "openai--whisper-medium.en");
        Directory.CreateDirectory(stt);
        File.WriteAllText(Path.Combine(stt, "model.safetensors"), "x");
        File.WriteAllText(Path.Combine(stt, "vocab.json"), "{}");
        File.WriteAllText(Path.Combine(stt, "merges.txt"), string.Empty);
        HartsyInferenceSpeechModel model = new(new TranscriptionOptions { ModelsRoot = folder.Path });

        SpeechModelException error = await Assert.ThrowsAsync<SpeechModelException>(
            () => model.TranscribeAsync(new SpeechWindow(0, new float[16_000]), CancellationToken.None));

        Assert.Equal("The speech model is not installed.", error.Message);
        Assert.False(error.Retryable);
        Assert.Equal(3, Directory.GetFiles(stt).Length);
    }
}
