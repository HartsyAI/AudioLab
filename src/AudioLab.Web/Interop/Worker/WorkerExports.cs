using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using AudioLab.Audio.Live;

namespace AudioLab.Web.Interop.Worker;

/// <summary>What the worker script calls in the engine host; compiled into the worker's runtime, not the page's.</summary>
/// <remarks>Everything here forwards to <see cref="LiveHost"/>. A JavaScript number is a double, so revisions and times arrive as one.</remarks>
[SupportedOSPlatform("browser")]
public static partial class WorkerExports
{
    private static LiveHost? _host;

    /// <summary>Builds the engine host: clips are fetched from the page's address, and chunks are rendered at the audio context's rate.</summary>
    [JSExport]
    public static void Start(string baseAddress, int sampleRate, int chunkFrames)
    {
        HttpClient http = new() { BaseAddress = new Uri(baseAddress) };
        _host = new LiveHost(new HttpClipSource(http), sampleRate, chunkFrames, _ => Task.Delay(1), CompiledAhead());
    }

    /// <summary>Makes a document, as <c>ProjectJson</c> writes it, the one to play.</summary>
    [JSExport]
    public static Task SetDocument(string json, double revision) => Host.SetDocumentAsync(json, (long)revision);

    /// <summary>Starts playing.</summary>
    [JSExport]
    public static void Play() => Host.Play();

    /// <summary>Pauses.</summary>
    [JSExport]
    public static void Pause() => Host.Pause();

    /// <summary>Stops and returns to where playing began.</summary>
    [JSExport]
    public static void Stop() => Host.Stop();

    /// <summary>Moves the playhead to a time in seconds.</summary>
    [JSExport]
    public static void Seek(double seconds) => Host.Seek(seconds);

    /// <summary>Turns the loop on or off over a range of seconds.</summary>
    [JSExport]
    public static void SetLoop(bool on, double startSeconds, double endSeconds) => Host.SetLoop(on, startSeconds, endSeconds);

    /// <summary>Renders the next chunk.</summary>
    [JSExport]
    public static byte[] RenderChunk() => Host.RenderChunk();

    /// <summary>The status as JSON, or nothing before <see cref="Start"/>.</summary>
    [JSExport]
    public static string ReadStatus() => _host?.ReadStatus() ?? string.Empty;

    // The worker project says in its own metadata whether it was published with ahead-of-time compilation (AudioLabWorkerAot).
    private static bool CompiledAhead() =>
        typeof(WorkerExports).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(item => item.Key == "AudioLabAot" && item.Value == "true");

    private static LiveHost Host => _host ?? throw new InvalidOperationException("The engine host has not been started.");
}
