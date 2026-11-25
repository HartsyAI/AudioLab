using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>The steps a key moves a selected point by, and the move that results (D69).</summary>
/// <remarks>A value step is a tenth of the drawn range, so a step is four pixels at the lane's height. The move names the point's index as the lane holds it now.</remarks>
public static class AutomationNudge
{
    /// <summary>How many value steps span the drawn range.</summary>
    public const double ValueSteps = 10;

    /// <summary>The move of a point one step in a direction, or null when the step changes nothing.</summary>
    /// <param name="points">The lane as it is now, in time order.</param>
    /// <param name="index">The index of the point.</param>
    /// <param name="parameter">The lane's parameter.</param>
    /// <param name="direction">The direction of the step.</param>
    /// <param name="timeStep">The grid step in seconds at the point's time, used by the time directions.</param>
    public static AutomationMoveTarget? Target(IReadOnlyList<AutomationPoint> points, int index, AutomationParameter parameter, NudgeDirection direction, double timeStep)
    {
        ArgumentNullException.ThrowIfNull(points);
        if ((uint)index >= (uint)points.Count)
        {
            return null;
        }
        AutomationPoint point = points[index];
        double range = AutomationRules.Max(parameter) - AutomationRules.Min(parameter);
        double time = direction switch
        {
            NudgeDirection.Earlier => Math.Max(0, point.Time - timeStep),
            NudgeDirection.Later => Math.Min(ProjectLimits.MaxSeconds, point.Time + timeStep),
            _ => point.Time,
        };
        double value = direction switch
        {
            NudgeDirection.Higher => AutomationRules.Clamp(parameter, point.Value + (range / ValueSteps)),
            NudgeDirection.Lower => AutomationRules.Clamp(parameter, point.Value - (range / ValueSteps)),
            _ => point.Value,
        };
        if (Math.Abs(time - point.Time) < 1e-9 && Math.Abs(value - point.Value) < 1e-9)
        {
            return null;
        }
        int landsAt = AutomationRules.Move(points, index, new AutomationPoint(time, value)).Index;
        return new AutomationMoveTarget(index, time, value, landsAt);
    }
}

