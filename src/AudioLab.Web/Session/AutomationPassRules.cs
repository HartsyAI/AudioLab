using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>The checks a recorded pass must pass before any of it is sent, so a refused pass changes nothing (D25).</summary>
public static class AutomationPassRules
{
    /// <summary>The reason a pass cannot be committed, or null when it can.</summary>
    /// <param name="pass">The pass to check.</param>
    /// <param name="parameter">The lane it is for.</param>
    public static string? Problem(AutomationPass pass, AutomationParameter parameter)
    {
        if (pass.Segments.Count == 0 || pass.Segments.Any(segment => segment.Count == 0))
        {
            return "The pass holds no points.";
        }
        int total = pass.Segments.Sum(segment => segment.Count);
        if (total > ProjectLimits.MaxListItems)
        {
            return $"The pass holds {total} points; a lane holds at most {ProjectLimits.MaxListItems}.";
        }
        double last = 0;
        foreach (IReadOnlyList<WrittenPoint> segment in pass.Segments)
        {
            foreach (WrittenPoint point in segment)
            {
                if (!double.IsFinite(point.Time) || point.Time < 0 || point.Time > ProjectLimits.MaxSeconds)
                {
                    return "A point of the pass is outside the timeline.";
                }
                if (!double.IsFinite(point.Value) || point.Value < AutomationRules.Min(parameter) || point.Value > AutomationRules.Max(parameter))
                {
                    return "A point of the pass is outside the range of the lane.";
                }
                if (point.Time < last)
                {
                    return "The points of the pass are not in time order.";
                }
                last = point.Time;
            }
        }
        return null;
    }
}
