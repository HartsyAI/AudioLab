namespace AudioLab.Web.Session;

/// <summary>Keeps the stand-in waveform path of each clip, so a new revision of the document does not rebuild hundreds of strings.</summary>
public sealed class WavePathCache
{
    private const int Capacity = 8192;

    private readonly Dictionary<(string Clip, int Points), string> _paths = [];

    /// <summary>How many paths are held.</summary>
    public int Count => _paths.Count;

    /// <summary>The placeholder path for a clip drawn over a body of about <paramref name="widthPixels"/> pixels.</summary>
    public string For(string clip, double widthPixels)
    {
        ArgumentNullException.ThrowIfNull(clip);
        int points = Math.Clamp((int)Math.Round(widthPixels / 2.6 / 4) * 4, 24, PlaceholderWaveform.MaxPoints);
        (string, int) key = (clip, points);
        if (!_paths.TryGetValue(key, out string? path))
        {
            if (_paths.Count >= Capacity)
            {
                _paths.Clear();
            }
            path = PlaceholderWaveform.Path(PlaceholderWaveform.Peaks(clip, points));
            _paths[key] = path;
        }
        return path;
    }
}
