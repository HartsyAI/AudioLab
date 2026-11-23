using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Waveforms;

/// <summary>Gives each audio clip the waveform it draws: nothing while the peaks load, a path from the file's peaks, or why there is none.</summary>
/// <remarks>
/// A path is built once for each window, level and size, and the same string is returned afterwards, so a clip whose waveform did not
/// change compares equal to what it drew and is not rendered again.
/// </remarks>
public sealed class WaveformService(PeakStore peaks)
{
    private const int Capacity = 8192;

    private readonly Dictionary<(string Media, int Level, double Offset, double Visible, int Columns), string> _paths = [];

    private readonly Lock _gate = new();

    /// <summary>Raised with the media id when a file's peaks arrive or fail, so what draws them can ask again.</summary>
    public event Action<string>? Arrived
    {
        add => peaks.Arrived += value;
        remove => peaks.Arrived -= value;
    }

    /// <summary>How many paths are held.</summary>
    public int Count => _paths.Count;

    /// <summary>The waveform of a clip drawn over a body of <paramref name="widthPixels"/> at <paramref name="pixelsPerSecond"/>.</summary>
    public ClipWave For(AudioClip clip, double widthPixels, double pixelsPerSecond)
    {
        ArgumentNullException.ThrowIfNull(clip);
        PeakView view = peaks.Get(clip.Media.Value, pixelsPerSecond);
        if (view.Phase == WavePhase.Failed)
        {
            return new ClipWave(WavePhase.Failed, null, view.Failure);
        }
        if (view is not { Phase: WavePhase.Ready, Header: PeakHeader header, Level: PeakLevel level })
        {
            return ClipWave.Loading;
        }
        double visible = ClipRules.VisibleLength(clip);
        int columns = WavePath.Columns(widthPixels);
        (string, int, double, double, int) key = (clip.Media.Value, level.FramesPerPeak, clip.Offset, visible, columns);
        lock (_gate)
        {
            if (!_paths.TryGetValue(key, out string? path))
            {
                if (_paths.Count >= Capacity)
                {
                    _paths.Clear();
                }
                path = WavePath.Build(level, header.Channels, header.SampleRate, clip.Offset, visible, columns);
                _paths[key] = path;
            }
            return new ClipWave(WavePhase.Ready, path);
        }
    }

    /// <summary>Asks again for the peaks of a clip's file when they failed, so selecting a clip whose waveform is missing tries once more.</summary>
    public void RetryIfFailed(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        peaks.Retry(clip.Media.Value);
    }
}
