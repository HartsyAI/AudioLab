using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AudioLab.Audio;
using AudioLab.Web.Bench;
using AudioLab.Web.Interop;

namespace AudioLab.Web.Playback;

/// <summary>One playback run: starts the engine where the options say, plays until the audio thread has played the asked number of minutes, and reports.</summary>
public sealed class PlaybackRun(IAudioBridge bridge, IBrowser browser, SpikeOptions options, HttpClient http, LoadRunner load)
{
    private const string ShimUrl = "audio-shim.js";

    private readonly TaskCompletionSource _workerBooted = new();

    private readonly TaskCompletionSource _workerReady = new();

    private readonly Stopwatch _wall = new();

    private AudioEngine? _engine;

    private ChunkRenderer? _renderer;

    private EngineSnapshot? _workerEngine;

    private ClipLoadResult? _clips;

    private int _capacity;

    private double _budgetMs;

    private bool _stalled;

    private string? _error;

    /// <summary>Raised when the numbers the status bar shows have changed.</summary>
    public event Action? Changed;

    /// <summary>The run's phase: idle, starting, running, done or failed.</summary>
    public string Phase { get; private set; } = "idle";

    /// <summary>The audio thread's latest counters.</summary>
    public WorkletStats Stats { get; private set; } = new();

    /// <summary>The engine's render load as a fraction of one core, from whichever thread hosts the engine.</summary>
    public double Utilisation => (_engine is null ? _workerEngine : EngineSnapshot.From(_engine.Load))?.Utilisation ?? 0;

    /// <summary>The sample rate once the context exists.</summary>
    public double SampleRate { get; private set; }

    /// <summary>Whether a run is in progress.</summary>
    public bool Busy => Phase is "starting" or "running";

    /// <summary>Starts the run, from a click, since a context only starts after a gesture.</summary>
    public async Task RunAsync()
    {
        if (Busy || options.Engine is not ("main" or "worker"))
        {
            return;
        }
        SetPhase("starting");
        _wall.Restart();
        try
        {
            await StartAsync();
            using CancellationTokenSource stopLoad = new();
            Task loading = options.Load ? load.RunAsync(stopLoad.Token) : Task.CompletedTask;
            await PlayAsync();
            await stopLoad.CancelAsync();
            await loading;
            SetPhase("done");
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or FormatException or JsonException)
        {
            _error = exception.ToString();
            SetPhase("failed");
        }
        await PostAsync(final: true);
    }

    private static int ChunksAsked(AudioMessage message) => (int)message.Number;

    private async Task StartAsync()
    {
        bridge.CreateContext();
        SampleRate = bridge.SampleRate;
        int rate = (int)SampleRate;
        _budgetMs = options.BufferMs;
        double usable = (options.BufferMs / 1000) - bridge.BaseLatency - bridge.OutputLatency;
        _capacity = (int)(usable * rate) / options.ChunkFrames * options.ChunkFrames;
        if (_capacity < 2 * options.ChunkFrames)
        {
            throw new InvalidOperationException($"{options.BufferMs} ms leaves no room for a ring after the context's own latency.");
        }
        await bridge.AddModuleAsync(ShimUrl);
        bridge.CreateNode(_capacity, options.ChunkFrames);
        bridge.OnNodeMessage(OnNodeMessage);
        await bridge.ResumeAsync();
        Uri origin = new(http.BaseAddress!, ".");
        if (options.Engine == "main")
        {
            ClipLoader loader = new(http);
            _clips = await loader.LoadAsync(ClipAddresses(origin), CancellationToken.None);
            _engine = StressMix.Build(rate, 128, loader.Clips);
            _renderer = new ChunkRenderer(_engine, options.ChunkFrames);
            bridge.StartMainThreadProducer();
        }
        else
        {
            bridge.CreateWorker(ShimUrl);
            bridge.OnWorkerMessage(OnWorkerMessage);
            await _workerBooted.Task;
            bridge.InitWorker(origin.ToString(), rate, options.ChunkFrames, options.MediaClips);
            await _workerReady.Task;
            bridge.ConnectWorkerToNode();
        }
    }

    private async Task PlayAsync()
    {
        SetPhase("running");
        long target = (long)(options.Minutes * 60 * SampleRate);
        int tick = 0;
        while (Stats.Counted < target)
        {
            await Task.Delay(1000);
            tick++;
            MaybeStall();
            if (tick % 5 == 0)
            {
                await PostAsync(final: false);
            }
            if (_error is not null)
            {
                throw new InvalidOperationException(_error);
            }
            if (_wall.Elapsed.TotalSeconds > (options.Minutes * 60 * 3) + 120)
            {
                throw new InvalidOperationException("The audio thread played far less than the wall clock allowed; the run was abandoned.");
            }
        }
    }

    private void MaybeStall()
    {
        if (_stalled || options.StallAtSeconds <= 0 || Stats.Counted < options.StallAtSeconds * SampleRate)
        {
            return;
        }
        _stalled = true;
        if (_renderer is not null)
        {
            _renderer.StallNext(options.StallMilliseconds);
        }
        else
        {
            bridge.StallWorker(options.StallMilliseconds);
        }
    }

    private Uri[] ClipAddresses(Uri origin)
    {
        Uri[] addresses = new Uri[options.MediaClips];
        for (int i = 0; i < addresses.Length; i++)
        {
            addresses[i] = new Uri(origin, $"media/clip-{i}.wav");
        }
        return addresses;
    }

    private void OnNodeMessage(AudioMessage message)
    {
        if (message.IsText)
        {
            Stats = JsonSerializer.Deserialize(message.Text!, PlaybackJsonContext.Default.WorkletStats) ?? Stats;
            Changed?.Invoke();
            return;
        }
        if (_renderer is null)
        {
            return;
        }
        for (int i = 0; i < ChunksAsked(message); i++)
        {
            bridge.PostChunk(_renderer.Render());
        }
    }

    private void OnWorkerMessage(AudioMessage message)
    {
        string text = message.Text ?? string.Empty;
        if (text == "booted")
        {
            _workerBooted.TrySetResult();
        }
        else if (text.StartsWith("ready:", StringComparison.Ordinal))
        {
            _clips = JsonSerializer.Deserialize(text.AsSpan(6), AudioJsonContext.Default.ClipLoadResult);
            _workerReady.TrySetResult();
        }
        else if (text.StartsWith("engine:", StringComparison.Ordinal))
        {
            _workerEngine = JsonSerializer.Deserialize(text.AsSpan(7), AudioJsonContext.Default.EngineSnapshot);
        }
        else if (text.StartsWith("error:", StringComparison.Ordinal))
        {
            _error = text;
            _workerBooted.TrySetException(new InvalidOperationException(text));
            _workerReady.TrySetException(new InvalidOperationException(text));
        }
    }

    private void SetPhase(string phase)
    {
        Phase = phase;
        Changed?.Invoke();
    }

    private async Task PostAsync(bool final)
    {
        double rate = SampleRate > 0 ? SampleRate : 48000;
        RunReport report = new()
        {
            Label = options.Label,
            Mode = options.Engine,
            Phase = Phase,
            Final = final,
            UserAgent = browser.UserAgent,
            CrossOriginIsolated = bridge.CrossOriginIsolated,
            SharedArrayBufferType = bridge.SharedArrayBufferType,
            InFrame = bridge.InFrame,
            SampleRate = SampleRate,
            BaseLatencyMs = SampleRate > 0 ? bridge.BaseLatency * 1000 : 0,
            OutputLatencyMs = SampleRate > 0 ? bridge.OutputLatency * 1000 : 0,
            BufferBudgetMs = _budgetMs,
            CapacityFrames = _capacity,
            CapacityMs = _capacity * 1000 / rate,
            MaxFillMs = Stats.MaxFill * 1000 / rate,
            ChunkFrames = options.ChunkFrames,
            WallSeconds = _wall.Elapsed.TotalSeconds,
            AudioSeconds = Stats.Frame / rate,
            Visibility = browser.VisibilityState,
            HasFocus = browser.HasFocus,
            HeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            Worklet = Stats,
            EngineLoad = _engine is null ? _workerEngine : EngineSnapshot.From(_engine.Load),
            Clips = _clips,
            PlaybackStats = SampleRate > 0 ? bridge.PlaybackStatsJson : null,
            Load = options.Load ? load.Summary : null,
            StallMilliseconds = _stalled ? options.StallMilliseconds : 0,
            Error = _error,
        };
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync($"report?label={Uri.EscapeDataString(options.Label)}", report, PlaybackJsonContext.Default.RunReport);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            _error ??= "report post failed: " + exception.Message;
        }
    }
}
