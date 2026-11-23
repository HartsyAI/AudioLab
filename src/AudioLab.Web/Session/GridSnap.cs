using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Snaps a time to the grid the lanes draw, so a clip that is dragged and a file that is dropped land on the same lines (D33).</summary>
public static class GridSnap
{
    /// <summary>The time on the grid nearest <paramref name="seconds"/>, never before the start.</summary>
    /// <remarks>
    /// A musical grid is counted in beats from the start, so every line sits on a subdivision of its bar whatever the tempo map does
    /// before it (the drawn bars come from the same map); a grid in seconds is counted in seconds.
    /// </remarks>
    public static double Seconds(TempoMap tempo, TimeSignature signature, string grid, double seconds)
    {
        ArgumentNullException.ThrowIfNull(tempo);
        if (GridRules.Beats(grid, signature) is { } step && step > 0)
        {
            double beats = Math.Round(tempo.BeatsAt(seconds) / step) * step;
            return Math.Max(0, tempo.TimeAtBeat(beats));
        }
        double length = GridRules.StepSeconds(grid, tempo, signature, seconds);
        return length > 0 ? Math.Round(seconds / length) * length : seconds;
    }
}
