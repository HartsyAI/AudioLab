namespace AudioLab.Web.Session;

/// <summary>SVG path data for each clip's waveform, built once from its cached peaks.</summary>
public sealed class WavePathCache
{
    private readonly Dictionary<int, string> _paths = [];

    /// <summary>The path data for a clip, building it on first use.</summary>
    public string For(ClipModel clip)
    {
        if (!_paths.TryGetValue(clip.Id, out string? path))
        {
            path = WaveformSynth.PathData(clip.Peaks);
            _paths[clip.Id] = path;
        }
        return path;
    }
}
