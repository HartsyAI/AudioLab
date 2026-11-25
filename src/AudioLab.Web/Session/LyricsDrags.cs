namespace AudioLab.Web.Session;

/// <summary>Whether a word of the lyrics is being dragged, so undo and redo wait until the drag ends.</summary>
public sealed class LyricsDrags
{
    private object? _owner;

    /// <summary>Whether a word is being dragged now.</summary>
    public bool Busy => _owner is not null;

    /// <summary>Marks a drag as begun by <paramref name="owner"/>.</summary>
    public void Begin(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Marks a drag as ended, when <paramref name="owner"/> began it.</summary>
    public void End(object owner)
    {
        if (ReferenceEquals(_owner, owner))
        {
            _owner = null;
        }
    }
}
