using AudioLab.Core.Transcription;
using HartsyInference.Core.Configuration;
using HartsyInference.Engine;
using HartsyInference.Engine.Dispatch;
using HartsyInference.Engine.Registry;
using HartsyInference.Engine.Requests;
using HartsyInference.Engine.Services;

namespace AudioLab.Server.Transcription;

/// <summary>One speech model, served by HartsyInference's Whisper pipeline on the machine's GPU when there is one (D98).</summary>
/// <remarks>
/// The model is read from <see cref="TranscriptionOptions.ModelsRoot"/> and never downloaded here; its folder and language come from its
/// registry entry. It is opened on the first window and stays resident until the server stops. The text comes back without times, so each
/// window's text is one segment spanning the audio the window was given. Only one native call runs at a time: a call still running after
/// its caller gave up holds the model, and a new call is refused as busy until it returns, so a retry never overlaps it.
/// </remarks>
public sealed class HartsyInferenceSpeechModel : ISpeechModel, IAsyncDisposable
{
    private const string NotInstalled = "The speech model is not installed.";
    private const string CouldNotOpen = "The speech model could not be opened.";
    private const string Failed = "The speech model failed.";
    private const string Busy = "The speech model is busy; it will be asked again.";

    private static readonly string[] _requiredFiles = ["model.safetensors", "vocab.json", "merges.txt", "config.json"];

    private readonly TranscriptionOptions _options;
    private readonly SpeechModelEntry _entry;
    private readonly ILogger<HartsyInferenceSpeechModel> _logger;
    private readonly Func<float[], string>? _native;
    private readonly SemaphoreSlim _open = new(1, 1);
    private readonly SemaphoreSlim _busy = new(1, 1);
    private InferenceEngine? _engine;
    private ITranscriberLease? _lease;

    /// <summary>Creates the adapter for one registry entry.</summary>
    public HartsyInferenceSpeechModel(TranscriptionOptions options, SpeechModelEntry entry, ILogger<HartsyInferenceSpeechModel> logger)
        : this(options, entry, logger, native: null)
    {
    }

    /// <summary>Creates the adapter with the native call given, so a test can stand in for the engine.</summary>
    internal HartsyInferenceSpeechModel(
        TranscriptionOptions options,
        SpeechModelEntry entry,
        ILogger<HartsyInferenceSpeechModel> logger,
        Func<float[], string>? native)
    {
        _options = options;
        _entry = entry;
        _logger = logger;
        _native = native;
    }

    /// <inheritdoc />
    public string ModelId => _entry.Id;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken)
    {
        float[] samples = window.Samples.ToArray();
        if (samples.Length == 0)
        {
            return [];
        }
        Func<float[], string> call = _native ?? await LeaseCallAsync(cancellationToken);
        if (!_busy.Wait(0))
        {
            throw new SpeechModelException(Busy, retryable: true);
        }
        // The native call is not given the token: it cannot be stopped, so the gate is released only when it returns (D93).
        Task<string> spoken = Task.Run(() =>
        {
            try
            {
                return call(samples);
            }
            finally
            {
                _busy.Release();
            }
        });
        string text;
        try
        {
            text = await spoken.WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The speech model call failed.");
            throw Map(exception);
        }
        string spokenText = text.Trim();
        if (spokenText.Length == 0)
        {
            return [];
        }
        double seconds = samples.Length / (double)TranscriptionWindows.SampleRate;
        return [new SpeechSegment(0, seconds, spokenText, SpeechKind.Speech)];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // A native call still running keeps the lease until it returns; the lease is never disposed under it.
        await _busy.WaitAsync();
        await _open.WaitAsync();
        try
        {
            _lease?.Dispose();
            _engine?.Dispose();
            _lease = null;
            _engine = null;
        }
        finally
        {
            _open.Release();
            _busy.Release();
        }
        _open.Dispose();
        _busy.Dispose();
    }

    private static SpeechModelException Map(Exception exception)
    {
        return exception is QueueFullException
            ? new SpeechModelException(Busy, retryable: true, exception)
            : new SpeechModelException(Failed, retryable: false, exception);
    }

    private async Task<Func<float[], string>> LeaseCallAsync(CancellationToken cancellationToken)
    {
        ITranscriberLease lease = await OpenAsync(cancellationToken);
        AudioRequest request = new()
        {
            Audio = new AudioClip { Data = [], Format = "wav" },
            Language = _entry.Language,
        };
        return samples => lease.Transcribe(samples, TranscriptionWindows.SampleRate, request);
    }

    private async Task<ITranscriberLease> OpenAsync(CancellationToken cancellationToken)
    {
        if (_lease is not null)
        {
            return _lease;
        }
        await _open.WaitAsync(cancellationToken);
        try
        {
            if (_lease is not null)
            {
                return _lease;
            }
            string root = CheckInstalled();
            KnobStore.Set(EngineKnobs.ModelsRoot, root);
            _engine = new InferenceEngine(_options.Device);
            ModelSpec spec = ModelResolver.Resolve(_entry.Id, null, Modality.Transcribe);
            _lease = await _engine.Transcribe.OpenTranscriberAsync(spec, cancellationToken);
            return _lease;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not SpeechModelException)
        {
            _logger.LogWarning(exception, "The speech model could not be opened.");
            throw new SpeechModelException(CouldNotOpen, retryable: false, exception);
        }
        finally
        {
            _open.Release();
        }
    }

    /// <summary>Returns the models root when every file the model needs is there; otherwise the job fails with the reason.</summary>
    private string CheckInstalled()
    {
        if (string.IsNullOrWhiteSpace(_options.ModelsRoot))
        {
            throw new SpeechModelException(NotInstalled, retryable: false);
        }
        string folder = Path.Combine(_options.ModelsRoot, "Audio", "stt", _entry.Folder);
        foreach (string file in _requiredFiles)
        {
            if (!File.Exists(Path.Combine(folder, file)))
            {
                throw new SpeechModelException(NotInstalled, retryable: false);
            }
        }
        return _options.ModelsRoot;
    }
}
