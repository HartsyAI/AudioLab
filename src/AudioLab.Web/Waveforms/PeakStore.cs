using System.Net;
using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Waveforms;

/// <summary>The peaks of the media files the page draws, fetched once for each file and level and held within a budget.</summary>
/// <remarks>
/// A file's header comes first; it says which levels exist, and the level a zoom needs is then asked for as its own byte range. A 503
/// (the server is rebuilding the peaks) is asked again after the time the server named, a few times; any other refusal fails the file
/// until <see cref="Retry"/>.
/// </remarks>
public sealed class PeakStore(MediaApi api, TimeProvider clock, ILogger<PeakStore> log, long budget = PeakStore.Budget)
{
    /// <summary>The most bytes of peak values the store keeps unless it is given another; the least recently drawn level goes first.</summary>
    public const long Budget = 64L * 1024 * 1024;

    /// <summary>How many requests are made for a header or a level, at most: a 503 is asked again until they are used up.</summary>
    public const int Attempts = 4;

    private static readonly TimeSpan _longestWait = TimeSpan.FromSeconds(10);

    // Four fetches at once leave the browser's connections for the hub and the other routes.
    private readonly SemaphoreSlim _slots = new(4);

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Entry> _entries = [];

    private long _bytes;

    private long _tick;

    /// <summary>Raised with the media id when a file's peaks, or why there are none, arrive.</summary>
    public event Action<string>? Arrived;

    /// <summary>How many bytes of peak values are held.</summary>
    public long HeldBytes => _bytes;

    /// <summary>What is known of a file's peaks at the zoom of <paramref name="pixelsPerSecond"/>, asking for what is missing.</summary>
    /// <remarks>A level that is ready is returned with its file's header; a file that is still loading, or failed, is returned as that.</remarks>
    public PeakView Get(string media, double pixelsPerSecond)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(media);
        lock (_gate)
        {
            if (!_entries.TryGetValue(media, out Entry? entry))
            {
                entry = new Entry();
                _entries[media] = entry;
            }
            if (entry.Failure is not null)
            {
                return new PeakView(WavePhase.Failed, null, null, entry.Failure);
            }
            if (entry.Header is not PeakHeader header)
            {
                Start(media, entry, null);
                return new PeakView(WavePhase.Loading, null, null, null);
            }
            int level = WaveformLevel.For([.. header.Levels.Select(item => item.FramesPerPeak)], header.SampleRate, pixelsPerSecond);
            if (entry.Levels.TryGetValue(level, out LevelHold? hold))
            {
                hold.LastUsed = ++_tick;
                return new PeakView(WavePhase.Ready, header, hold.Level, null);
            }
            Start(media, entry, level);
            return new PeakView(WavePhase.Loading, null, null, null);
        }
    }

    /// <summary>Asks again for a file whose peaks failed; nothing happens for one that did not.</summary>
    public void Retry(string media)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(media);
        lock (_gate)
        {
            if (!_entries.TryGetValue(media, out Entry? entry) || entry.Failure is null)
            {
                return;
            }
            entry.Failure = null;
        }
        Arrived?.Invoke(media);
    }

    // One fetch of a header or level at a time for each file: a second ask while it runs is the same fetch.
    private void Start(string media, Entry entry, int? level)
    {
        if (!entry.Running.Add(level ?? -1))
        {
            return;
        }
        _ = FetchAsync(media, entry, level);
    }

    private async Task FetchAsync(string media, Entry entry, int? level)
    {
        string? failure = null;
        PeakHeader? header = null;
        PeakLevel? fetched = null;
        for (int attempt = 1; attempt <= Attempts && failure is null && header is null && fetched is null; attempt++)
        {
            try
            {
                // The slot is held for the request only, never for the wait that follows a 503, so a busy file does not hold up the others.
                await _slots.WaitAsync();
                try
                {
                    if (level is int wanted)
                    {
                        fetched = await api.GetPeakLevelAsync(media, entry.Header!, wanted);
                    }
                    else
                    {
                        header = await api.GetPeakHeaderAsync(media);
                    }
                }
                finally
                {
                    _slots.Release();
                }
            }
            catch (ApiException busy) when (busy.Status == HttpStatusCode.ServiceUnavailable && attempt < Attempts)
            {
                await Task.Delay(Wait(busy), clock);
            }
            catch (Exception exception)
            {
                // No token is passed, so a cancellation here is the client's own timeout: a failure like any other, never a clip stuck loading.
                PeakLog.FetchFailed(log, exception);
                failure = FailureText.Of(exception);
            }
        }
        Store(entry, level, header, fetched, failure);
        lock (_gate)
        {
            entry.Running.Remove(level ?? -1);
        }
        Arrived?.Invoke(media);
    }

    private static TimeSpan Wait(ApiException busy)
    {
        return busy.RetryAfter is TimeSpan asked && asked < _longestWait ? asked : _longestWait;
    }

    private void Store(Entry entry, int? level, PeakHeader? header, PeakLevel? fetched, string? failure)
    {
        lock (_gate)
        {
            if (failure is not null)
            {
                entry.Failure = failure;
            }
            else if (header is not null)
            {
                entry.Header = header;
            }
            else if (fetched is not null && level is int index)
            {
                entry.Levels[index] = new LevelHold(fetched) { LastUsed = ++_tick };
                _bytes += fetched.Values.Length * sizeof(short);
                Trim(entry, index);
            }
        }
    }

    // Drops the least recently drawn levels, never the one just stored, until the store is within its budget.
    private void Trim(Entry keepEntry, int keepLevel)
    {
        while (_bytes > budget)
        {
            (Entry Owner, int Level, LevelHold Hold)? oldest = null;
            foreach (Entry owner in _entries.Values)
            {
                foreach ((int index, LevelHold hold) in owner.Levels)
                {
                    bool kept = owner == keepEntry && index == keepLevel;
                    if (!kept && (oldest is null || hold.LastUsed < oldest.Value.Hold.LastUsed))
                    {
                        oldest = (owner, index, hold);
                    }
                }
            }
            if (oldest is not { } drop)
            {
                return;
            }
            drop.Owner.Levels.Remove(drop.Level);
            _bytes -= drop.Hold.Level.Values.Length * sizeof(short);
        }
    }

    private sealed class Entry
    {
        public PeakHeader? Header { get; set; }

        public string? Failure { get; set; }

        public Dictionary<int, LevelHold> Levels { get; } = [];

        public HashSet<int> Running { get; } = [];
    }

    private sealed class LevelHold(PeakLevel level)
    {
        public PeakLevel Level { get; } = level;

        public long LastUsed { get; set; }
    }
}
