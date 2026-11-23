namespace AudioLab.Web.Session.Roll;

/// <summary>The gestures of the piano roll, held where the layers that draw them can see them; only the overlay and lanes redraw on a move.</summary>
/// <remarks>
/// A gesture is <see cref="Current"/> while the pointer is down. On release it becomes a landing gesture: its preview stays where it landed
/// until its edit is answered, but it no longer holds the pointer, so the next press does not wait for the round trip (O14).
/// </remarks>
public sealed class RollDragState
{
    private readonly List<IRollGesture> _landing = [];

    /// <summary>Raised when a gesture begins, moves, lands or ends.</summary>
    public event Action? Changed;

    /// <summary>The gesture the pointer is down in, or null.</summary>
    public IRollGesture? Current { get; private set; }

    /// <summary>The gestures that were released and whose edits have not been answered, oldest first.</summary>
    public IReadOnlyList<IRollGesture> Landing => _landing;

    /// <summary>Whether a pointer is down or an edit is on its way: history keys wait until it is not.</summary>
    public bool Busy => Current is not null || _landing.Count > 0;

    /// <summary>The gestures to draw: those landing, then the one the pointer is in.</summary>
    public IEnumerable<IRollGesture> Shown => Current is { } current ? [.. _landing, current] : _landing;

    /// <summary>The gesture to draw of a kind: the one the pointer is in, else the latest that is landing.</summary>
    public T? Latest<T>()
        where T : class, IRollGesture
    {
        return Current as T ?? _landing.OfType<T>().LastOrDefault();
    }

    /// <summary>Begins a gesture.</summary>
    public void Begin(IRollGesture gesture)
    {
        Current = gesture;
        Changed?.Invoke();
    }

    /// <summary>Tells the overlay the gesture moved.</summary>
    public void Moved() => Changed?.Invoke();

    /// <summary>The pointer was released and the edit is on its way: the gesture stops holding the pointer and keeps its preview.</summary>
    public void Land(IRollGesture gesture)
    {
        if (ReferenceEquals(gesture, Current))
        {
            Current = null;
        }
        _landing.Add(gesture);
        Changed?.Invoke();
    }

    /// <summary>Ends a gesture, whether it was answered or cancelled; another that began since goes on.</summary>
    public void End(IRollGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        bool removed = _landing.Remove(gesture);
        if (ReferenceEquals(gesture, Current))
        {
            Current = null;
            removed = true;
        }
        if (removed)
        {
            Changed?.Invoke();
        }
    }
}
