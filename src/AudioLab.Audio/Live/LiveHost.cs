using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AudioLab.Audio.Instruments;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;

namespace AudioLab.Audio.Live;

/// <summary>What the page's worker runs: it keeps the plan current with the project, fetches the clips it needs and renders chunks.</summary>
/// <remarks>
/// One thread: every member is called from the worker's event loop, so a call is never interrupted except at an await. The newest document
/// wins: one that arrives while an older one is still preparing replaces it, and a failed build leaves the plan that was playing. Fetching and
/// resampling a clip run in slices with a pause between them, so a chunk is never late by more than one slice; building a plan from cached
/// media is a few milliseconds, and an edit that needs a clip not yet held waits for it while the old plan keeps playing.
/// </remarks>
public sealed class LiveHost
{
    /// <summary>Bytes before the samples of a chunk: the timeline frame of its first sample as a little-endian double, then 32 bits of flags and 32 spare.</summary>
    public const int HeaderBytes = 16;

    /// <summary>The flag in a chunk header that says the chunk is audio the listener is due, so that missing it is a dropout.</summary>
    public const int AudibleFlag = 1;

    private const int LoadAttempts = 3;

    // What of a document does not change a plan, so that renaming the project or toggling the click does not rebuild it.
    private static readonly ProjectView _blankView = new() { Ruler = RulerMode.Beats, EditMode = EditMode.Slip, Grid = "1/4" };

    private readonly IClipSource _clips;
    private readonly Func<CancellationToken, Task> _pause;
    private readonly LiveEngine _engine;
    private readonly MediaCache _cache;
    private readonly InstrumentSourceFactory _instruments = new();
    private readonly byte[] _chunk;
    private readonly int _chunkFrames;
    private readonly int _rate;
    private readonly bool _compiledAhead;
    private ProjectDocument? _wanted;
    private ProjectDocument? _built;
    private long _wantedRevision = -1;
    private string? _problem;
    private int _loading;
    private bool _pumping;
    private double _maxRenderMs;
    private double _longestGapMs;
    private long _lastRender;

    /// <summary>Creates a host that renders chunks of <paramref name="chunkFrames"/> frames at <paramref name="sampleRate"/>.</summary>
    public LiveHost(IClipSource clips, int sampleRate, int chunkFrames, Func<CancellationToken, Task> pause, bool compiledAhead = false)
    {
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(pause);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkFrames, 1);
        _clips = clips;
        _pause = pause;
        _compiledAhead = compiledAhead;
        _rate = sampleRate;
        _chunkFrames = chunkFrames;
        _engine = new LiveEngine(sampleRate, chunkFrames);
        _cache = new MediaCache(NoMedia.Instance, sampleRate);
        _chunk = new byte[HeaderBytes + (chunkFrames * 2 * sizeof(float))];
    }

    /// <summary>The engine this host drives.</summary>
    public LiveEngine Engine => _engine;

    /// <summary>Makes the project at <paramref name="revision"/> the one to play; the task ends when the newest is in place or failed.</summary>
    /// <remarks>A revision not above the newest seen is ignored. An unreadable document is reported through the status.</remarks>
    public async Task SetDocumentAsync(string json, long revision)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (revision <= _wantedRevision)
        {
            return;
        }
        ProjectDocument document;
        try
        {
            document = ProjectJson.Read(json);
        }
        catch (JsonException exception)
        {
            _problem = $"The project could not be read: {exception.Message}";
            return;
        }
        _wanted = document;
        _wantedRevision = revision;
        _engine.SetClick(document.Metronome);
        if (_pumping)
        {
            return;
        }
        _pumping = true;
        try
        {
            await PumpAsync();
        }
        finally
        {
            _pumping = false;
        }
    }

    /// <summary>Starts playing from the playhead.</summary>
    public void Play() => _engine.Play();

    /// <summary>Stops playing and leaves the playhead where it is.</summary>
    public void Pause() => _engine.Pause();

    /// <summary>Stops playing and returns the playhead to where playing began.</summary>
    public void Stop() => _engine.Stop();

    /// <summary>Moves the playhead to a time in seconds.</summary>
    public void Seek(double seconds) => _engine.Seek(Frames(seconds));

    /// <summary>Turns the loop on or off over a range of seconds.</summary>
    public void SetLoop(bool on, double startSeconds, double endSeconds) => _engine.SetLoop(on, Frames(startSeconds), Frames(endSeconds));

    /// <summary>Renders the next chunk: <see cref="HeaderBytes"/> of header, then left and right as 32-bit floats; the array is reused.</summary>
    public byte[] RenderChunk()
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastRender != 0)
        {
            _longestGapMs = Math.Max(_longestGapMs, (now - _lastRender) * 1000.0 / Stopwatch.Frequency);
        }
        _lastRender = now;
        BinaryPrimitives.WriteDoubleLittleEndian(_chunk, _engine.Position);
        BinaryPrimitives.WriteInt32LittleEndian(_chunk.AsSpan(8), 0);
        Span<float> samples = MemoryMarshal.Cast<byte, float>(_chunk.AsSpan(HeaderBytes));
        _engine.Render(samples[.._chunkFrames], samples[_chunkFrames..]);
        BinaryPrimitives.WriteInt32LittleEndian(_chunk.AsSpan(8), _engine.Audible ? AudibleFlag : 0);
        return _chunk;
    }

    /// <summary>What the page is told: the transport, the plan and how the rendering is going; a reading clears the longest render.</summary>
    public string ReadStatus()
    {
        RenderStats stats = _engine.TakeStats();
        _maxRenderMs = Math.Max(_maxRenderMs, stats.MaxMilliseconds);
        MeterReading levels = _engine.TakeLevels();
        int count = levels.Tracks.Count;
        TrackLevel[] tracks = new TrackLevel[count];
        for (int i = 0; i < count; i++)
        {
            tracks[i] = new TrackLevel(levels.Tracks[i].Value, new LevelPair(levels.Peaks[i * 2], levels.Peaks[(i * 2) + 1]));
        }
        LevelPair master = count == 0 ? default : new LevelPair(levels.Peaks[count * 2], levels.Peaks[(count * 2) + 1]);
        HostStatus status = new(
            _engine.State, _engine.Position, _rate, _engine.Revision, _loading, _problem, stats.Chunks, stats.Overruns, _maxRenderMs,
            _longestGapMs, master, tracks, _compiledAhead);
        _maxRenderMs = 0;
        _longestGapMs = 0;
        return JsonSerializer.Serialize(status, PlaybackJsonContext.Default.HostStatus);
    }

    private long Frames(double seconds) => (long)Math.Max(0, Math.Round(seconds * _rate, MidpointRounding.AwayFromZero));

    private async Task PumpAsync()
    {
        while (_wanted is { } document)
        {
            long revision = _wantedRevision;
            _wanted = null;
            if (_built is { } built && Key(built).Equals(Key(document)))
            {
                continue;
            }
            if (await BuildAsync(document, revision))
            {
                _built = document;
            }
        }
    }

    private static ProjectDocument Key(ProjectDocument document) => document with { Name = string.Empty, Metronome = false, View = _blankView };

    // Makes a plan, fetching what it is missing first; false when it could not, so the plan that was playing stays.
    private async Task<bool> BuildAsync(ProjectDocument document, long revision)
    {
        PlanOptions options = new() { SampleRate = _rate, Cache = _cache, Sources = _instruments };
        for (int attempt = 0; attempt < LoadAttempts; attempt++)
        {
            try
            {
                RenderPlan plan = RenderPlan.Create(document, NoMedia.Instance, options);
                _engine.Install(plan, revision, new ClickGenerator(document, _rate));
                _problem = null;
                return true;
            }
            catch (MissingMediaException missing)
            {
                if (!await LoadAsync(missing.Missing) || _wanted is not null)
                {
                    return false;
                }
            }
        }
        _problem = "The clips could not all be held in memory at once.";
        return false;
    }

    private async Task<bool> LoadAsync(IReadOnlyList<MediaId> ids)
    {
        _loading = ids.Count;
        try
        {
            foreach (MediaId id in ids)
            {
                PcmAudio decoded = await _clips.LoadAsync(id, _pause, CancellationToken.None);
                await _cache.AddAsync(id, decoded, _pause, CancellationToken.None);
                _loading--;
            }
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or WavFormatException or ArgumentException)
        {
            _problem = $"A clip could not be loaded: {exception.Message}";
            return false;
        }
        finally
        {
            _loading = 0;
        }
    }
}
