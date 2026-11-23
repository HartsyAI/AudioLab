using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Keeps the preview path of one MIDI clip and builds it again only when its notes changed, so a redraw or a lifted copy costs nothing.</summary>
public sealed class MidiPathMemo
{
    private MidiClip? _clip;

    /// <summary>The path of the clip last given to <see cref="Update"/>.</summary>
    public string Path { get; private set; } = string.Empty;

    /// <summary>How many times the path was built.</summary>
    public int Builds { get; private set; }

    /// <summary>Takes a clip; true when it differs from the last one (by value) and the path was built again.</summary>
    public bool Update(MidiClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (_clip is not null && (ReferenceEquals(_clip, clip) || _clip.Equals(clip)))
        {
            return false;
        }
        _clip = clip;
        Path = MidiPreview.Path(clip);
        Builds++;
        return true;
    }
}
