
namespace AudioLab.Web.Session;

/// <summary>Decides whether an automation lane must draw again: the first draw is remembered, and a later one draws only on a change.</summary>
public sealed class AutomationLaneGate
{
    private AutomationLaneShown? _last;

    /// <summary>Remembers what was drawn, for a first render, which does not ask for a decision.</summary>
    public void Remember(AutomationLaneShown shown) => _last = shown;

    /// <summary>Whether the lane must draw again, which also remembers what it is about to draw.</summary>
    public bool Draw(AutomationLaneShown shown)
    {
        bool same = _last is { } last
            && ReferenceEquals(last.Points, shown.Points)
            && last with { Points = null } == shown with { Points = null };
        _last = shown;
        return !same;
    }
}
