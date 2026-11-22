namespace AudioLab.Web.Session;

/// <summary>Chooses how far apart the labels of the time ruler are, so they stay readable at any scale.</summary>
public static class TimeTicks
{
    private const double MinimumLabelSpacing = 64;

    private static readonly double[] _steps = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];

    /// <summary>The seconds between labels: the smallest step whose labels are at least 64 pixels apart.</summary>
    public static double Step(TimelineGeometry geometry)
    {
        foreach (double step in _steps)
        {
            if (geometry.Width(step) >= MinimumLabelSpacing)
            {
                return step;
            }
        }
        return _steps[^1];
    }

    /// <summary>The times that carry a label, from 0 up to <paramref name="endSeconds"/>.</summary>
    public static IEnumerable<double> Times(TimelineGeometry geometry, double endSeconds)
    {
        double step = Step(geometry);
        for (double time = 0; time <= endSeconds; time += step)
        {
            yield return time;
        }
    }
}
