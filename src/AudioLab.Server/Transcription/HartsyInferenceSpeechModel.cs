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
/// The model is read from its folder under <see cref="TranscriptionOptions.ModelsRoot"/>, the folder the registry entry names, and never
/// downloaded here; the engine is given that same folder. It is opened on the first window and stays resident until the server stops. The
/// text comes back without times, so each window's text is one segment spanning the audio the window was given. Only one native call runs
/// at a time: a call still running after its caller gave up holds the model, and a later call waits for it, inside its own model timeout.
/// </remarks>
public sealed class HartsyInferenceSpeechModel : ISpeechModel, IAsyncDisposable
{
    private const string NotInstalled = "The speech model is not installed.";
    private const string CouldNotOpen = "The speech model could not be opened.";
    private const string Failed = "The speech model failed.";
    private const string Busy = "The speech model is busy; it will be asked again.";
    private static readonly TimeSpan _disposeWait = TimeSpan.FromSeconds(1);

    private static readonly string[] _requiredFiles = ["model.safetensors", "vocab.json", "merges.txt", "config.json"];

    private readonly TranscriptionOptions _options;
    private readonly SpeechModelEntry _entry;
    private readonly ILogger<HartsyInferenceSpeechModel> _logger;
    private readonly Func<float[], AudioRequest, string>? _native;
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
        Func<float[], AudioRequest, string>? native)
    {
        _options = options;
        _entry = entry;
        _logger = logger;
        _native = native;
    }

    /// <inheritdoc />
    public string ModelId => _entry.Id;

    /// <inheritdoc />
    public bool IsBusy => _busy.CurrentCount == 0;

    /// <summary>The folder the entry's model lives in, under the models root.</summary>
    public static string ModelFolder(TranscriptionOptions options, SpeechModelEntry entry)
    {
        return Path.Combine(options.ModelsRoot ?? string.Empty, "Audio", "stt", entry.Folder);
    }

    /// <summary>Whether the folder of a registry entry holds every file the adapter needs. A model that is not installed is not registered,
    /// so it cannot be chosen (D98).</summary>
    public static bool IsInstalled(TranscriptionOptions options, SpeechModelEntry entry)
    {
        if (string.IsNullOrWhiteSpace(options.ModelsRoot))
        {
            return false;
        }
        string folder = ModelFolder(options, entry);
        return _requiredFiles.All(file => File.Exists(Path.Combine(folder, file)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeechSegment>> TranscribeAsync(SpeechWindow window, CancellationToken cancellationToken)
    {
        if (!IsInstalled(_options, _entry))
        {
            throw new SpeechModelException(NotInstalled, retryable: false);
        }
        float[] samples = window.Samples.ToArray();
        if (samples.Length == 0)
        {
            return [];
        }
        AudioRequest request = new()
        {
            Audio = new AudioClip { Data = [], Format = "wav" },
            Language = _entry.Language,
        };
        Func<float[], AudioRequest, string> call = _native ?? await LeaseCallAsync(cancellationToken);
        // A call the caller gave up on may still hold the model; this one waits for it, inside the model timeout (D93).
        await _busy.WaitAsync(cancellationToken);
        // The native call is not given the token: it cannot be stopped, so the gate is released only when it returns.
        Task<string> spoken = Task.Run(() =>
        {
            try
            {
                return call(samples, request);
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
            TranscriptionLog.ModelCallFailed(_logger, exception);
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
        // A native call or a model open still running keeps the lease. Both waits share one deadline of one second, so the stop's total stays
        // within the host's shutdown timeout (D93); a lease still in use is left to the process.
        using CancellationTokenSource bound = new(_disposeWait);
        bool holdsBusy = false;
        try
        {
            await _busy.WaitAsync(bound.Token);
            holdsBusy = true;
            await _open.WaitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            if (holdsBusy)
            {
                _busy.Release();
            }
            TranscriptionLog.ModelStillBusy(_logger);
            return;
        }
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
    }

    private static SpeechModelException Map(Exception exception)
    {
        return exception is QueueFullException
            ? new SpeechModelException(Busy, retryable: true, exception)
            : new SpeechModelException(Failed, retryable: false, exception);
    }

    private async Task<Func<float[], AudioRequest, string>> LeaseCallAsync(CancellationToken cancellationToken)
    {
        ITranscriberLease lease = await OpenAsync(cancellationToken);
        return (samples, request) => lease.Transcribe(samples, TranscriptionWindows.SampleRate, request);
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
            KnobStore.Set(EngineKnobs.ModelsRoot, _options.ModelsRoot!);
            InferenceEngine engine = new(_options.Device);
            try
            {
                // The engine is given the folder the entry names, the same one IsInstalled checked.
                ModelSpec spec = ModelResolver.Resolve(_entry.Id, ModelFolder(_options, _entry), Modality.Transcribe);
                ITranscriberLease lease = await engine.Transcribe.OpenTranscriberAsync(spec, cancellationToken);
                _engine = engine;
                _lease = lease;
                return lease;
            }
            catch
            {
                // A failed or cancelled open leaves nothing behind: the next call opens again, with an engine of its own.
                engine.Dispose();
                throw;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not SpeechModelException)
        {
            TranscriptionLog.ModelOpenFailed(_logger, exception);
            throw new SpeechModelException(CouldNotOpen, retryable: false, exception);
        }
        finally
        {
            _open.Release();
        }
    }
}
