using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AudioLab.Audio.Instruments;
using AudioLab.Audio.Wav;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;

namespace AudioLab.Audio.Live;

/// <summary>What the page's worker runs: it keeps the plan current with the project, fetches the clips it needs and renders chunks.</summary>
/// <remarks>
/// One thread: every member is called from the worker's event loop, so a call is never interrupted except at an await. The newest document
/// wins: one that arrives while an older one is still preparing replaces it, and a failed build leaves the plan that was playing. Fetching,
/// decoding and resampling a clip run in slices with a pause between them; what does not (reading the document, the header check of a
/// file) is a single step of some milliseconds that can make a chunk late. A plan is built a track at a time with a pause between tracks
/// (<see cref="RenderPlan.CreateAsync"/>), so what a chunk can wait for is the dearest track (a reverb), not the whole plan; a build whose
/// document a newer one has replaced is abandoned at the next pause. Every failure of a build is a problem the status reports, and none of
/// them touches the transport.
/// </remarks>
public sealed class LiveHost
{
    /// <summary>Bytes before the samples of a chunk: two little-endian doubles, the frame of its first sample and 1 for audio due, else 0.</summary>
    public const int HeaderBytes = 16;

    private const int LoadAttempts = 3;

    private readonly IClipSource _clips;
    private readonly Func<CancellationToken, Task> _pause;
    private readonly LiveEngine _engine;
    private readonly InstrumentSourceFactory _instruments = new();
    private readonly byte[] _chunk;
    private readonly int _chunkFrames;
    private readonly int _rate;
    private readonly bool _compiledAhead;
    private readonly MediaCache _cache;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _buildCancellation;
    private MediaId? _loadingClip;
    private Dictionary<MediaId, string> _names = [];
    private TrackLevel[] _trackLevels = [];
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

    /// <summary>Replaces how effects become inserts, so a test can make building a plan cost what it chooses.</summary>
    internal Func<Effect, InsertContext, IInsert?>? InsertBuilder { get; init; }

    /// <summary>The clock that tells a build when to give the worker a turn; a test supplies its own.</summary>
    internal TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>Makes the project at <paramref name="revision"/> the one to play; the task ends when the newest is in place or failed.</summary>
    /// <remarks>A revision not above the newest seen is ignored. A document or a clip that cannot be played is a problem in the status.</remarks>
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
        catch (ProjectFormatException exception)
        {
            _problem = $"The project could not be read: {exception.Message}";
            return;
        }
        _wanted = document;
        _wantedRevision = revision;
        _engine.SetClick(document.Metronome);
        // A plan that is being built from an older document is of no use once a newer one has come.
        _buildCancellation?.Cancel();
        if (_loadingClip is { } loading && !ClipNames(document).ContainsKey(loading))
        {
            // The clip being fetched is not in the newer document: no reason to wait for it.
            _loadCancellation?.Cancel();
        }
        if (_pumping)
        {
            return;
        }
        _pumping = true;
        try
        {
            await PumpAsync().ConfigureAwait(false);
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
        Span<float> samples = MemoryMarshal.Cast<byte, float>(_chunk.AsSpan(HeaderBytes));
        _engine.Render(samples[.._chunkFrames], samples[_chunkFrames..]);
        BinaryPrimitives.WriteDoubleLittleEndian(_chunk.AsSpan(8), _engine.Audible ? 1 : 0);
        return _chunk;
    }

    /// <summary>What the page is told: the transport, the plan and how the rendering is going; a reading clears the longest render.</summary>
    public string ReadStatus()
    {
        RenderStats stats = _engine.TakeStats();
        _maxRenderMs = Math.Max(_maxRenderMs, stats.MaxMilliseconds);
        MeterReading levels = _engine.TakeLevels();
        int count = levels.Tracks.Count;
        if (_trackLevels.Length != count)
        {
            _trackLevels = new TrackLevel[count];
        }
        for (int i = 0; i < count; i++)
        {
            _trackLevels[i] = new TrackLevel(levels.Tracks[i].Value, new LevelPair(levels.Peaks[i * 2], levels.Peaks[(i * 2) + 1]));
        }
        LevelPair master = count == 0 ? default : new LevelPair(levels.Peaks[count * 2], levels.Peaks[(count * 2) + 1]);
        HostStatus status = new(
            _engine.State, _engine.Position, _rate, _engine.Revision, _loading, _problem, stats.Chunks, stats.Overruns, _maxRenderMs,
            _longestGapMs, master, _trackLevels, _compiledAhead);
        _maxRenderMs = 0;
        _longestGapMs = 0;
        return JsonSerializer.Serialize(status, PlaybackJsonContext.Default.HostStatus);
    }

    private static Dictionary<MediaId, string> ClipNames(ProjectDocument document)
    {
        Dictionary<MediaId, string> names = [];
        foreach (AudioClip clip in document.Tracks.SelectMany(track => track.Clips))
        {
            names.TryAdd(clip.Media, clip.Name);
        }
        return names;
    }

    private long Frames(double seconds) => (long)Math.Max(0, Math.Round(seconds * _rate, MidpointRounding.AwayFromZero));

    private async Task PumpAsync()
    {
        while (_wanted is { } document)
        {
            long revision = _wantedRevision;
            _wanted = null;
            ProjectDocument key = PlanKey.Of(document);
            if (_built is { } built && built.Equals(key))
            {
                continue;
            }
            try
            {
                if (await BuildAsync(document, revision).ConfigureAwait(false))
                {
                    _built = key;
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // The edge of what the worker does for the page: whatever a build throws becomes a problem, and the old plan keeps playing.
                _problem = $"The project could not be played: {exception.Message}";
                _loading = 0;
            }
        }
    }

    // Makes a plan, fetching what it is missing first; false when it could not, so the plan that was playing stays.
    private async Task<bool> BuildAsync(ProjectDocument document, long revision)
    {
        _names = ClipNames(document);
        for (int attempt = 0; attempt < LoadAttempts; attempt++)
        {
            using CancellationTokenSource cancellation = new();
            _buildCancellation = cancellation;
            PlanOptions options = new()
            {
                SampleRate = _rate,
                Cache = _cache,
                Sources = _instruments,
                InsertBuilder = InsertBuilder,
                CancellationToken = cancellation.Token,
            };
            try
            {
                // The old plan plays on while the new one is built a track at a time, with the worker's loop free in between to render chunks.
                RenderPlan plan = await RenderPlan.CreateAsync(document, NoMedia.Instance, options, _pause, Time).ConfigureAwait(false);
                _engine.Install(plan, revision, new ClickGenerator(document, _rate));
                _problem = null;
                return true;
            }
            catch (OperationCanceledException) when (_wanted is not null)
            {
                // A newer document came during the build: the old plan plays on until the build of that one is done.
                return false;
            }
            catch (MissingMediaException missing)
            {
                if (!await LoadAsync(missing.Missing).ConfigureAwait(false) || _wanted is not null)
                {
                    return false;
                }
            }
            catch (ArgumentException exception)
            {
                _problem = $"The project cannot be played: {exception.Message}";
                return false;
            }
            finally
            {
                _buildCancellation = null;
            }
        }
        _problem = "The clips could not all be held in memory at once.";
        return false;
    }

    private async Task<bool> LoadAsync(IReadOnlyList<MediaId> ids)
    {
        _loading = ids.Count;
        MediaId current = default;
        try
        {
            foreach (MediaId id in ids)
            {
                if (_wanted is not null)
                {
                    return false;
                }
                current = id;
                using CancellationTokenSource cancellation = new();
                _loadCancellation = cancellation;
                _loadingClip = id;
                PcmAudio decoded = await _clips.LoadAsync(id, _pause, cancellation.Token);
                await _cache.AddAsync(id, decoded, _pause, cancellation.Token);
                _loading--;
            }
            return true;
        }
        catch (OperationCanceledException) when (_wanted is not null)
        {
            return false;
        }
        catch (Exception exception) when (exception is ClipUnavailableException or WavFormatException or HttpRequestException
            or OperationCanceledException or OverflowException or ArgumentException or InvalidDataException)
        {
            string name = _names.GetValueOrDefault(current) ?? current.Value;
            _problem = $"The clip \"{name}\" {Reason(exception)}";
            return false;
        }
        finally
        {
            _loading = 0;
            _loadingClip = null;
            _loadCancellation = null;
        }
    }

    // A clip source says what is wrong in words that follow the clip's name; anything else gets the plainest wording that fits.
    private static string Reason(Exception exception) => exception switch
    {
        ClipUnavailableException => exception.Message + ".",
        WavFormatException => "is not a WAV file the engine can read: " + exception.Message,
        OperationCanceledException => "timed out while it was fetched.",
        _ => "could not be loaded: " + exception.Message,
    };
}
