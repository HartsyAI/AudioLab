using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Waveforms;

/// <summary>Gives each audio clip the waveform it draws: nothing while the peaks load, a path from the file's peaks, or why there is none.</summary>
/// <remarks>
/// A path is built once for each window and size and the same string is returned afterwards, so a clip whose waveform did not change compares equal
/// to what it drew. The paths are asked first: a level of peaks that was dropped for room is not fetched again for a clip that already has its path.
/// </remarks>
public sealed class WaveformService(PeakStore peaks, long characterBudget = WaveformService.CharacterBudget)
{
    /// <summary>The most characters of paths the service keeps, about sixteen megabytes; the least recently drawn go first.</summary>
    public const long CharacterBudget = 8_000_000;

    private readonly Dictionary<(string Media, double Offset, double Visible, int Columns, double Zoom), Held> _paths = [];

    private readonly Lock _gate = new();

    private long _chars;

    private long _tick;

    /// <summary>Raised with the media id when a file's peaks arrive or fail, so what draws them can ask again.</summary>
    public event Action<string>? Arrived
    {
        add => peaks.Arrived += value;
        remove => peaks.Arrived -= value;
    }

    /// <summary>How many paths are held.</summary>
    public int Count => _paths.Count;

    /// <summary>How many characters of paths are held.</summary>
    public long Characters => _chars;

    /// <summary>The waveform of a clip drawn over a body of <paramref name="widthPixels"/> at <paramref name="pixelsPerSecond"/>.</summary>
    public ClipWave For(AudioClip clip, double widthPixels, double pixelsPerSecond)
    {
        ArgumentNullException.ThrowIfNull(clip);
        double visible = ClipRules.VisibleLength(clip);
        int columns = WavePath.Columns(widthPixels);
        (string, double, double, int, double) key = (clip.Media.Value, clip.Offset, visible, columns, pixelsPerSecond);
        lock (_gate)
        {
            if (_paths.TryGetValue(key, out Held? held))
            {
                held.LastUsed = ++_tick;
                return new ClipWave(WavePhase.Ready, held.Path);
            }
        }
        PeakView view = peaks.Get(clip.Media.Value, pixelsPerSecond);
        if (view.Phase == WavePhase.Failed)
        {
            return new ClipWave(WavePhase.Failed, null, view.Failure);
        }
        if (view is not { Phase: WavePhase.Ready, Header: PeakHeader header, Level: PeakLevel level })
        {
            return ClipWave.Loading;
        }
        string path = WavePath.Build(level, header.Channels, header.SampleRate, clip.Offset, visible, columns);
        lock (_gate)
        {
            _paths[key] = new Held(path) { LastUsed = ++_tick };
            _chars += path.Length;
            Trim(key);
        }
        return new ClipWave(WavePhase.Ready, path);
    }

    /// <summary>Asks again for the peaks of a clip's file when they failed, so selecting a clip whose waveform is missing tries once more.</summary>
    public void RetryIfFailed(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        peaks.Retry(clip.Media.Value);
    }

    // Over budget, the least recently drawn paths go until a quarter of the budget is free, so the next draw does not trim again.
    private void Trim(object keep)
    {
        if (_chars <= characterBudget)
        {
            return;
        }
        long target = characterBudget * 3 / 4;
        foreach (KeyValuePair<(string, double, double, int, double), Held> entry in _paths.OrderBy(pair => pair.Value.LastUsed).ToList())
        {
            if (_chars <= target)
            {
                return;
            }
            if (!Equals(entry.Key, keep))
            {
                _paths.Remove(entry.Key);
                _chars -= entry.Value.Path.Length;
            }
        }
    }

    private sealed class Held(string path)
    {
        public string Path { get; } = path;

        public long LastUsed { get; set; }
    }
}
