using AudioLab.Core.Transcription;
using HartsyInference.Core.Configuration;
using HartsyInference.Engine;
using HartsyInference.Engine.Dispatch;
using HartsyInference.Engine.Registry;
using HartsyInference.Engine.Requests;
using HartsyInference.Engine.Services;

namespace AudioLab.Server.Transcription;

/// <summary>The speech model, served by HartsyInference's Whisper pipeline on the machine's GPU when there is one (D98).</summary>
/// <remarks>The model is read from <see cref="TranscriptionOptions.ModelsRoot"/> and never downloaded here. It is opened once, on
/// the first window, and stays resident until the server stops. The text comes back without times, so each window's text is one
/// segment spanning the audio the window was given.</remarks>
public sealed class HartsyInferenceSpeechModel(TranscriptionOptions options) : ISpeechModel, IAsyncDisposable
{
    private const string NotInstalled = "The speech model is not installed.";
    private const string ModelFolder = "openai--whisper-medium.en";

    private static readonly string[] _requiredFiles = ["model.safetensors", "vocab.json", "merges.txt", "config.json"];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceEngine? _engine;
    private ITranscriberLease? _lease;

    /// <inheritdoc />
    public string ModelId => options.ModelId;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken)
    {
        ITranscriberLease lease = await OpenAsync(cancellationToken);
        float[] samples = window.Samples.ToArray();
        if (samples.Length == 0)
        {
            return [];
        }
        string text;
        try
        {
            text = await Task.Run(() => lease.Transcribe(samples, TranscriptionWindows.SampleRate, Request), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SpeechModelException($"The speech model failed: {exception.Message}", retryable: false);
        }
        string spoken = text.Trim();
        if (spoken.Length == 0)
        {
            return [];
        }
        double seconds = samples.Length / (double)TranscriptionWindows.SampleRate;
        return [new SpeechSegment(0, seconds, spoken, SpeechKind.Speech)];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _lease?.Dispose();
            _engine?.Dispose();
            _lease = null;
            _engine = null;
        }
        finally
        {
            _gate.Release();
        }
        _gate.Dispose();
    }

    private static AudioRequest Request => new()
    {
        Audio = new AudioClip { Data = [], Format = "wav" },
        Language = "en",
    };

    private async Task<ITranscriberLease> OpenAsync(CancellationToken cancellationToken)
    {
        if (_lease is not null)
        {
            return _lease;
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_lease is not null)
            {
                return _lease;
            }
            string root = CheckInstalled();
            KnobStore.Set(EngineKnobs.ModelsRoot, root);
            _engine = new InferenceEngine(options.Device);
            ModelSpec spec = ModelResolver.Resolve(options.ModelId, null, Modality.Transcribe);
            _lease = await _engine.Transcribe.OpenTranscriberAsync(spec, cancellationToken);
            return _lease;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not SpeechModelException)
        {
            throw new SpeechModelException($"The speech model could not be opened: {exception.Message}", retryable: false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Returns the models root when every file the model needs is there; otherwise the job fails with the reason.</summary>
    private string CheckInstalled()
    {
        if (string.IsNullOrWhiteSpace(options.ModelsRoot))
        {
            throw new SpeechModelException(NotInstalled, retryable: false);
        }
        string folder = Path.Combine(options.ModelsRoot, "Audio", "stt", ModelFolder);
        foreach (string file in _requiredFiles)
        {
            if (!File.Exists(Path.Combine(folder, file)))
            {
                throw new SpeechModelException(NotInstalled, retryable: false);
            }
        }
        return options.ModelsRoot;
    }
}
