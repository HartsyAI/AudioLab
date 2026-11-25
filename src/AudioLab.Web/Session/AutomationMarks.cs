using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>The marks a lane draws over its line: the selected point, and the dragged point with its neighbours (D70).</summary>
public static class AutomationMarks
{
    /// <summary>The selected point as a mark, or nothing when no point of the lane is selected.</summary>
    /// <param name="plan">The lane's plan.</param>
    /// <param name="points">The lane's points, in time order.</param>
    /// <param name="index">The selected point's index, or null.</param>
    public static string Selected(AutomationLanePlan plan, IReadOnlyList<AutomationPoint> points, int? index)
    {
        if (index is not { } at || at >= points.Count)
        {
            return string.Empty;
        }
        AutomationPoint point = points[at];
        return $"M{Format(plan.X(point.Time))} {Format(plan.Y(point.Value))}h0";
    }

    /// <summary>The dragged point where it would be released, joined to its neighbours; nothing when no drag is in progress.</summary>
    /// <param name="plan">The lane's plan.</param>
    /// <param name="points">The lane's points as the document holds them, which the drag started from.</param>
    /// <param name="drag">The drag in progress, or null.</param>
    public static string Overlay(AutomationLanePlan plan, IReadOnlyList<AutomationPoint> points, AutomationDrag? drag)
    {
        if (drag is null)
        {
            return string.Empty;
        }
        double x = plan.X(drag.Time);
        double y = plan.Y(drag.Value);
        string path = $"M{Format(x)} {Format(y)}";
        if (drag.Index > 0 && drag.Index - 1 < points.Count)
        {
            AutomationPoint before = points[drag.Index - 1];
            path = $"M{Format(plan.X(before.Time))} {Format(plan.Y(before.Value))}L{Format(x)} {Format(y)}";
        }
        if (drag.Index + 1 < points.Count)
        {
            AutomationPoint after = points[drag.Index + 1];
            path += $"L{Format(plan.X(after.Time))} {Format(plan.Y(after.Value))}";
        }
        return path;
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
