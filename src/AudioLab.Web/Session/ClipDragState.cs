namespace AudioLab.Web.Session;

/// <summary>The drag in progress, held where the lanes and the drag layer can both see it; only the drag layer redraws on a move.</summary>
public sealed class ClipDragState
{
    /// <summary>Raised when a drag begins or ends, and on every move of it.</summary>
    public event Action? Changed;

    /// <summary>The drag in progress, or null.</summary>
    public ClipDrag? Current { get; private set; }

    /// <summary>Begins a drag.</summary>
    public void Begin(ClipDrag drag)
    {
        Current = drag;
        Changed?.Invoke();
    }

    /// <summary>Tells the drag layer the drag moved.</summary>
    public void Moved() => Changed?.Invoke();

    /// <summary>Ends the drag, whether it was released or cancelled.</summary>
    /// <param name="drag">The drag that ends; when another drag has begun since, that one goes on. Null ends whatever is in progress.</param>
    public void End(ClipDrag? drag = null)
    {
        if (drag is not null && !ReferenceEquals(drag, Current))
        {
            return;
        }
        Current = null;
        Changed?.Invoke();
    }
}
