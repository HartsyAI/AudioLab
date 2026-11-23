namespace AudioLab.Web.Media;

/// <summary>The media drag in progress, held where the lanes and the lifted label can both see it.</summary>
public sealed class MediaDragState
{
    /// <summary>Raised when a drag begins or ends, and on every move of it.</summary>
    public event Action? Changed;

    /// <summary>The drag in progress, or null.</summary>
    public MediaDrag? Current { get; private set; }

    /// <summary>Begins a drag.</summary>
    public void Begin(MediaDrag drag)
    {
        Current = drag;
        Changed?.Invoke();
    }

    /// <summary>Tells the layers the drag moved.</summary>
    public void Moved() => Changed?.Invoke();

    /// <summary>Ends the drag, whether it was released or cancelled.</summary>
    public void End(MediaDrag? drag = null)
    {
        if (drag is not null && !ReferenceEquals(drag, Current))
        {
            return;
        }
        Current = null;
        Changed?.Invoke();
    }
}
