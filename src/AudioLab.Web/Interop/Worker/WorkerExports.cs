using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using AudioLab.Audio;

namespace AudioLab.Web.Interop.Worker;

/// <summary>What the worker's shim calls: load the clips, build the engine, render chunks. Compiled into the worker host, not into the page.</summary>
[SupportedOSPlatform("browser")]
public static partial class WorkerExports
{
    private static AudioEngine? _engine;

    private static ChunkRenderer? _renderer;

    /// <summary>Fetches the clips, builds the session and returns the load result as JSON.</summary>
    [JSExport]
    public static async Task<string> StartAsync(string origin, int sampleRate, int chunkFrames, int clipCount)
    {
        using HttpClient http = new();
        Uri[] addresses = new Uri[clipCount];
        for (int i = 0; i < clipCount; i++)
        {
            addresses[i] = new Uri(new Uri(origin), $"media/clip-{i}.wav");
        }
        ClipLoader loader = new(http);
        ClipLoadResult result = await loader.LoadAsync(addresses, CancellationToken.None);
        _engine = StressMix.Build(sampleRate, 128, loader.Clips);
        _renderer = new ChunkRenderer(_engine, chunkFrames);
        return JsonSerializer.Serialize(result, AudioJsonContext.Default.ClipLoadResult);
    }

    /// <summary>Renders the next chunk; the array is the same one every call.</summary>
    [JSExport]
    public static byte[] RenderChunk() => (_renderer ?? throw new InvalidOperationException("The engine has not been started.")).Render();

    /// <summary>Makes the next render hold the worker for this many milliseconds.</summary>
    [JSExport]
    public static void StallNext(int milliseconds) => (_renderer ?? throw new InvalidOperationException("The engine has not been started.")).StallNext(milliseconds);

    /// <summary>The render meter as JSON.</summary>
    [JSExport]
    public static string ReadSnapshot()
    {
        AudioEngine engine = _engine ?? throw new InvalidOperationException("The engine has not been started.");
        return JsonSerializer.Serialize(EngineSnapshot.From(engine.Load), AudioJsonContext.Default.EngineSnapshot);
    }
}
