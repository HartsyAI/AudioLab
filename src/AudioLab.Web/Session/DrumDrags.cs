namespace AudioLab.Web.Session;

/// <summary>The paint drags of the drums tab that have not finished. While one has not, undo and redo wait, as for a clip or a word drag.</summary>
public sealed class DrumDrags
{
    private int _open;

    /// <summary>Whether a paint drag has not finished, so that history waits.</summary>
    public bool Busy => _open > 0;

    /// <summary>Counts a paint drag that has begun.</summary>
    public void Begin() => _open++;

    /// <summary>Counts a paint drag that has finished, once its gesture is closed.</summary>
    public void End() => _open = Math.Max(0, _open - 1);
}
