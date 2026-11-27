namespace AudioLab.Web.Interop;

/// <summary>The handlers of the one drag a pointer follows: a new drag replaces the drag in progress, and the one it replaces is cancelled, never left wedged (D86).</summary>
public sealed class PointerDrag
{
    /// <summary>The handler of a pointer move, or null when no drag is followed.</summary>
    public Action<double, double>? Move { get; private set; }

    /// <summary>The handler of a release, or null when no drag is followed.</summary>
    public Action<double, double>? End { get; private set; }

    /// <summary>The handler of a cancel, or null when no drag is followed.</summary>
    public Action? Cancel { get; private set; }

    /// <summary>Follows a new drag and returns the cancel of the drag it replaced, which the caller invokes once the new drag is listening.</summary>
    /// <param name="move">The handler of a pointer move.</param>
    /// <param name="end">The handler of a release.</param>
    /// <param name="cancel">The handler of a cancel.</param>
    /// <returns>The cancel of the drag that was in progress, or null.</returns>
    public Action? Begin(Action<double, double> move, Action<double, double> end, Action cancel)
    {
        Action? displaced = Cancel;
        (Move, End, Cancel) = (move, end, cancel);
        return displaced;
    }

    /// <summary>Forgets the drag in progress, if any, without calling any of its handlers.</summary>
    public void Clear() => (Move, End, Cancel) = (null, null, null);
}
