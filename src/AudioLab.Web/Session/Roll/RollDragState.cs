namespace AudioLab.Web.Session.Roll;

/// <summary>The gesture in progress in the piano roll, held where the layers that draw it can see it; only the overlay redraws on a move.</summary>
public sealed class RollDragState
{
    /// <summary>Raised when a gesture begins or ends, and on every move of it.</summary>
    public event Action? Changed;

    /// <summary>The gesture in progress, or null.</summary>
    public IRollGesture? Current { get; private set; }

    /// <summary>Begins a gesture.</summary>
    public void Begin(IRollGesture gesture)
    {
        Current = gesture;
        Changed?.Invoke();
    }

    /// <summary>Tells the overlay the gesture moved.</summary>
    public void Moved() => Changed?.Invoke();

    /// <summary>Ends the gesture, whether it was released or cancelled; another that began since goes on.</summary>
    public void End(IRollGesture? gesture = null)
    {
        if (gesture is not null && !ReferenceEquals(gesture, Current))
        {
            return;
        }
        Current = null;
        Changed?.Invoke();
    }
}
