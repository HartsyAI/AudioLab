using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Maps a lane's times and values to its pixels, and draws its line as the engine plays it (D35): straight between points.</summary>
/// <remarks>
/// The line is drawn with one segment group per pixel column, so its size is bounded by the lane's width and not by its point count. Points
/// are found by a search over their times, not by one element per point. Volume is drawn from 0 to <see cref="AutomationRules.VolumeDisplayMax"/>,
/// as D25 allows; a value above that is drawn at the top and kept as it is.
/// </remarks>
public sealed class AutomationLanePlan(AutomationParameter parameter, TimelineGeometry geometry, double height)
{
    /// <summary>How far from a point, in pixels, a press still hits it.</summary>
    public const double HitRadius = 5;

    /// <summary>The parameter the lane shows.</summary>
    public AutomationParameter Parameter { get; } = parameter;

    /// <summary>The height of the lane in pixels.</summary>
    public double Height { get; } = height;

    /// <summary>The lowest value the lane draws.</summary>
    public double Min => AutomationRules.Min(Parameter);

    /// <summary>The highest value the lane draws: volume's display maximum, pan's one.</summary>
    public double Max => Parameter == AutomationParameter.Volume ? AutomationRules.VolumeDisplayMax : AutomationRules.Max(Parameter);

    /// <summary>The pixel of a time, from the left of the lanes.</summary>
    public double X(double time) => geometry.X(time);

    /// <summary>The time at a pixel, clamped to the project's limit.</summary>
    public double Time(double x) => Math.Clamp(geometry.Seconds(x), 0, ProjectLimits.MaxSeconds);

    /// <summary>The pixel of a value, with the top of the lane as the highest.</summary>
    public double Y(double value) => Height * (1 - ((Math.Clamp(value, Min, Max) - Min) / (Max - Min)));

    /// <summary>The value at a pixel, clamped to the lane's drawn range.</summary>
    public double Value(double y) => Math.Clamp(Min + ((1 - (Math.Clamp(y, 0, Height) / Height)) * (Max - Min)), Min, Max);

    /// <summary>The line of a lane as one SVG path across <paramref name="width"/> pixels; its size is at most four segments a pixel column.</summary>
    /// <param name="points">The lane's points, in time order.</param>
    /// <param name="width">The width of the lane in pixels.</param>
    /// <returns>The path. Outside the points the lane holds its end value.</returns>
    public string Path(IReadOnlyList<AutomationPoint> points, double width)
    {
        ArgumentNullException.ThrowIfNull(points);
        StringBuilder path = new();
        double first = points.Count == 0 ? AutomationRules.Identity(Parameter) : points[0].Value;
        double lastX = 0;
        double lastY = Y(first);
        Append(path, 'M', lastX, lastY);
        foreach (ColumnGroup group in Columns(points, width))
        {
            double[] ys = [Y(group.First), Y(group.Low), Y(group.High), Y(group.Last)];
            foreach (double y in ys)
            {
                if (group.Column != lastX || y != lastY)
                {
                    Append(path, 'L', group.Column, y);
                    lastX = group.Column;
                    lastY = y;
                }
            }
        }
        Append(path, 'H', width, null);
        return path.ToString();
    }

    /// <summary>One small mark at each pixel column that holds a point, as one path, so a lane of any size is one element.</summary>
    /// <param name="points">The lane's points, in time order.</param>
    /// <param name="width">The width of the lane in pixels.</param>
    /// <returns>The path of the marks.</returns>
    public string Marks(IReadOnlyList<AutomationPoint> points, double width)
    {
        ArgumentNullException.ThrowIfNull(points);
        StringBuilder path = new();
        foreach (ColumnGroup group in Columns(points, width))
        {
            Append(path, 'M', group.Column, Y(group.First));
            path.Append("h0");
        }
        return path.ToString();
    }

    /// <summary>The index of the point a pixel is on, the nearest one within <see cref="HitRadius"/>, or null.</summary>
    /// <param name="points">The lane's points, in time order.</param>
    /// <param name="x">The pixel across the lanes.</param>
    /// <param name="y">The pixel down the lane.</param>
    public int? IndexAt(IReadOnlyList<AutomationPoint> points, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(points);
        int low = 0;
        int high = points.Count;
        double left = x - HitRadius;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (X(points[middle].Time) < left)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        int? best = null;
        double bestDistance = double.MaxValue;
        for (int index = low; index < points.Count && X(points[index].Time) <= x + HitRadius; index++)
        {
            double dx = X(points[index].Time) - x;
            double dy = Y(points[index].Value) - y;
            double distance = (dx * dx) + (dy * dy);
            if (distance <= HitRadius * HitRadius && distance < bestDistance)
            {
                best = index;
                bestDistance = distance;
            }
        }
        return best;
    }

    // The points grouped by the pixel column they fall in, in order; each group keeps its first, lowest, highest and last value.
    private IEnumerable<ColumnGroup> Columns(IReadOnlyList<AutomationPoint> points, double width)
    {
        int? column = null;
        ColumnGroup group = default;
        foreach (AutomationPoint point in points)
        {
            double x = Math.Clamp(X(point.Time), 0, width);
            int at = (int)Math.Floor(x);
            if (column is { } current && current == at)
            {
                group = group with { Low = Math.Min(group.Low, point.Value), High = Math.Max(group.High, point.Value), Last = point.Value };
                continue;
            }
            if (column is not null)
            {
                yield return group;
            }
            column = at;
            group = new ColumnGroup(at, point.Value, point.Value, point.Value, point.Value);
        }
        if (column is not null)
        {
            yield return group;
        }
    }

    private static void Append(StringBuilder path, char command, double x, double? y)
    {
        path.Append(command).Append(Number(x));
        if (y is { } at)
        {
            path.Append(' ').Append(Number(at));
        }
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private readonly record struct ColumnGroup(int Column, double First, double Low, double High, double Last);
}
