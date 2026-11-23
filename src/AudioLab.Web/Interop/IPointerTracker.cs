namespace AudioLab.Web.Interop;

/// <summary>Follows the pointer across the whole page while a drag lasts, so a drag needs neither one element under it nor pointer capture.</summary>
public interface IPointerTracker
{
    /// <summary>Starts following: <paramref name="move"/> on a move, <paramref name="end"/> on release, <paramref name="cancel"/> on Esc.</summary>
    /// <remarks>Positions are in client pixels.</remarks>
    public void Begin(Action<double, double> move, Action<double, double> end, Action cancel);

    /// <summary>Stops following, without calling any of the three.</summary>
    public void Stop();
}
