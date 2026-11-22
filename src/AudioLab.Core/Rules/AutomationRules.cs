using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The automation rules of the old DAW: a lane is a list of points by time, read as a piecewise-linear envelope.</summary>
public static class AutomationRules
{
    /// <summary>The highest value of a volume lane: the fader's own maximum, which is what the validator accepts.</summary>
    public const double VolumeMax = ProjectLimits.MaxGain;

    /// <summary>The top of the volume lane as the old DAW drew it; a view may scale the lane to this and clamp a drag there.</summary>
    public const double VolumeDisplayMax = 2;

    /// <summary>How far before a written pass its lead-in point sits, in seconds, so the old curve holds until just before.</summary>
    public const double LeadIn = 0.01;

    /// <summary>How far after a written pass the old curve takes over again, in seconds.</summary>
    public const double TailOut = 0.05;

    /// <summary>Two values or times closer than this are the same.</summary>
    public const double Epsilon = 1e-4;

    /// <summary>The lowest value of a lane.</summary>
    public static double Min(AutomationParameter parameter) => parameter == AutomationParameter.Volume ? 0 : -1;

    /// <summary>The highest value of a lane.</summary>
    public static double Max(AutomationParameter parameter) => parameter == AutomationParameter.Volume ? VolumeMax : 1;

    /// <summary>The value that changes nothing: times one for volume, no offset for pan.</summary>
    public static double Identity(AutomationParameter parameter) => parameter == AutomationParameter.Volume ? 1 : 0;

    /// <summary>The value held inside the lane's range.</summary>
    public static double Clamp(AutomationParameter parameter, double value) => Math.Clamp(value, Min(parameter), Max(parameter));

    /// <summary>A track's points of one lane.</summary>
    public static ValueList<AutomationPoint> Lane(TrackAutomation automation, AutomationParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(automation);
        return parameter == AutomationParameter.Volume ? automation.Volume : automation.Pan;
    }

    /// <summary>The automation with one lane replaced.</summary>
    public static TrackAutomation WithLane(TrackAutomation automation, AutomationParameter parameter, ValueList<AutomationPoint> points)
    {
        ArgumentNullException.ThrowIfNull(automation);
        return parameter == AutomationParameter.Volume ? automation with { Volume = points } : automation with { Pan = points };
    }

    /// <summary>The envelope's value at a time: the first value before the first point, the last after the last, a straight line between.</summary>
    /// <remarks>Null for a lane with no points, which leaves the control alone.</remarks>
    public static double? ValueAt(IReadOnlyList<AutomationPoint> points, double time)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            return null;
        }
        if (time <= points[0].Time)
        {
            return points[0].Value;
        }
        if (time >= points[^1].Time)
        {
            return points[^1].Value;
        }
        // The first point at or after the time; the lane is in time order, so a search finds it.
        int low = 1;
        int high = points.Count - 1;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (points[middle].Time >= time)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }
        AutomationPoint a = points[low - 1];
        AutomationPoint b = points[low];
        double span = b.Time - a.Time;
        return a.Value + ((b.Value - a.Value) * ((time - a.Time) / (span == 0 ? 1e-9 : span)));
    }

    /// <summary>The lane with a point added; among points at the same time it goes last. The index says where it landed.</summary>
    public static LaneEdit Insert(IReadOnlyList<AutomationPoint> points, AutomationPoint point)
    {
        ArgumentNullException.ThrowIfNull(points);
        int at = 0;
        while (at < points.Count && points[at].Time <= point.Time)
        {
            at++;
        }
        List<AutomationPoint> list = [.. points];
        list.Insert(at, point);
        return new LaneEdit([.. list], at);
    }

    /// <summary>The lane with one point put at a new time and value, in time order. The index says where it landed.</summary>
    /// <remarks>
    /// Among points at the new time it keeps the place nearest to where it was, so a point moved within a step stays where it is.
    /// A point dragged across the points of a step can land on the other side of them, which swaps the step's two values.
    /// </remarks>
    public static LaneEdit Move(IReadOnlyList<AutomationPoint> points, int index, AutomationPoint point)
    {
        ArgumentNullException.ThrowIfNull(points);
        List<AutomationPoint> rest = [.. points];
        rest.RemoveAt(index);
        int earliest = 0;
        while (earliest < rest.Count && rest[earliest].Time < point.Time)
        {
            earliest++;
        }
        int latest = earliest;
        while (latest < rest.Count && rest[latest].Time <= point.Time)
        {
            latest++;
        }
        int at = Math.Clamp(index, earliest, latest);
        rest.Insert(at, point);
        return new LaneEdit([.. rest], at);
    }

    /// <summary>The points a grab on the line at a time moves: the last point at or before it and the first one after it.</summary>
    /// <remarks>Each is -1 when there is none.</remarks>
    public static (int Left, int Right) Segment(IReadOnlyList<AutomationPoint> points, double time)
    {
        ArgumentNullException.ThrowIfNull(points);
        int left = -1;
        for (int i = 0; i < points.Count && points[i].Time <= time; i++)
        {
            left = i;
        }
        return (left, left + 1 < points.Count ? left + 1 : -1);
    }

    /// <summary>Folds a recorded pass into a lane: the old curve leads in and the written points replace what was under them.</summary>
    /// <remarks>
    /// With <paramref name="returnToOld"/> the old curve takes over again just after the pass. Inside the pass, of two points closer
    /// than <see cref="Epsilon"/> in time (but not at the same time) the later one stays, and a held value keeps only the ends of its
    /// run; the rest of the lane is not touched. A pass that wrote what was already there returns the lane as it was.
    /// </remarks>
    public static ValueList<AutomationPoint> Commit(
        ValueList<AutomationPoint> lane,
        IReadOnlyList<AutomationPoint> written,
        AutomationParameter parameter,
        bool returnToOld)
    {
        ArgumentNullException.ThrowIfNull(written);
        if (written.Count == 0)
        {
            return lane;
        }
        List<AutomationPoint> pass = [.. written.OrderBy(point => point.Time)];
        double At(double time) => ValueAt(lane, time) ?? Identity(parameter);
        if (pass.All(point => Math.Abs(point.Value - At(point.Time)) < Epsilon))
        {
            return lane;
        }
        double first = pass[0].Time;
        double last = pass[^1].Time;
        double lead = Math.Max(0, first - LeadIn);
        double spanStart = first > LeadIn ? lead : first;
        double tail = Math.Min(last + TailOut, ProjectLimits.MaxSeconds);
        double before = At(lead);
        double after = At(tail);
        double keepUntil = returnToOld ? tail : last;
        List<AutomationPoint> replacement = [];
        if (first > LeadIn)
        {
            replacement.Add(new AutomationPoint(lead, before));
        }
        replacement.AddRange(pass);
        if (returnToOld)
        {
            replacement.Add(new AutomationPoint(tail, after));
        }
        List<AutomationPoint> sorted = [.. replacement.OrderBy(point => point.Time)];
        List<AutomationPoint> unique = [];
        foreach (AutomationPoint point in sorted)
        {
            // Two samples a hair apart are one: the later is what was played. Two at one time are a step and both stay.
            double gap = unique.Count == 0 ? double.MaxValue : point.Time - unique[^1].Time;
            if (gap > 0 && gap <= Epsilon)
            {
                unique[^1] = point;
            }
            else
            {
                unique.Add(point);
            }
        }
        // Only the span of the pass is tidied: a step or a held run elsewhere in the lane is the user's and stays as it is.
        return
        [
            .. lane.Where(point => point.Time < spanStart),
            .. unique.Where((point, i) => IsEnd(unique, i)),
            .. lane.Where(point => point.Time > keepUntil),
        ];
    }

    // A held value is a run of equal points: only its ends carry information.
    private static bool IsEnd(List<AutomationPoint> points, int i)
    {
        return i == 0 || i == points.Count - 1
            || Math.Abs(points[i].Value - points[i - 1].Value) > Epsilon
            || Math.Abs(points[i].Value - points[i + 1].Value) > Epsilon;
    }
}
