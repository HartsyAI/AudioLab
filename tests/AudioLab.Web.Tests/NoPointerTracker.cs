using AudioLab.Web.Interop;

namespace AudioLab.Web.Tests;

/// <summary>A pointer tracker that follows nothing, for a render that only draws.</summary>
internal sealed class NoPointerTracker : IPointerTracker
{
    /// <inheritdoc />
    public bool Shift => false;

    /// <inheritdoc />
    public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
    {
    }

    /// <inheritdoc />
    public void Stop()
    {
    }
}
