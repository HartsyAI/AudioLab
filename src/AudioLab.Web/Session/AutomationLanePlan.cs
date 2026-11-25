using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Maps a lane's times and values to its pixels, and draws its line the way the engine reads it (D35).</summary>
/// <remarks>Volume is straight between points and pan holds each point's value until the next one. Outside the points a lane holds its end value.</remarks>
public sealed class AutomationLanePlan(AutomationParameter parameter, TimelineGeometry geometry, double height)
{
    /// <summary>The parameter the lane shows.</summary>
    public AutomationParameter Parameter { get; } = parameter;

    /// <summary>The height of the lane in pixels.</summary>
    public double Height { get; } = height;

    /// <summary>The lowest value the lane shows.</summary>
    public double Min => AutomationRules.Min(Parameter);

    /// <summary>The highest value the lane shows.</summary>
    public double Max => AutomationRules.Max(Parameter);

    /// <summary>The pixel of a time, from the left of the lanes.</summary>
    public double X(double time) => geometry.X(time);

    /// <summary>The time at a pixel, clamped to the project's limit.</summary>
    public double Time(double x) => Math.Clamp(geometry.Seconds(x), 0, ProjectLimits.MaxSeconds);

    /// <summary>The pixel of a value, with the top of the lane as the highest.</summary>
    public double Y(double value) => Height * (1 - ((AutomationRules.Clamp(Parameter, value) - Min) / (Max - Min)));

    /// <summary>The value at a pixel, clamped to the lane's range.</summary>
    public double Value(double y) => AutomationRules.Clamp(Parameter, Min + ((1 - (Math.Clamp(y, 0, Height) / Height)) * (Max - Min)));

    /// <summary>The line of a lane as one SVG path, across <paramref name="width"/> pixels.</summary>
    public string Path(IReadOnlyList<AutomationPoint> points, double width)
    {
        ArgumentNullException.ThrowIfNull(points);
        StringBuilder path = new();
        double first = points.Count == 0 ? AutomationRules.Identity(Parameter) : points[0].Value;
        path.Append('M').Append(Number(0)).Append(' ').Append(Number(Y(first)));
        bool hold = Parameter == AutomationParameter.Pan;
        foreach (AutomationPoint point in points)
        {
            double x = Math.Min(width, X(point.Time));
            if (hold)
            {
                path.Append('H').Append(Number(x));
            }
            path.Append('L').Append(Number(x)).Append(' ').Append(Number(Y(point.Value)));
        }
        path.Append('H').Append(Number(width));
        return path.ToString();
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
