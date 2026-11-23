using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>The edit grid as the lanes draw it, so a drag and a click land on the same lines.</summary>
/// <remarks>
/// A musical grid is counted in beats from the start through the tempo map, so every line sits on a subdivision of its bar whatever the
/// tempo does before it; a grid in seconds is counted in seconds.
/// </remarks>
public sealed class GridSnap
{
    private readonly TempoMap _tempo;

    private readonly TimeSignature _signature;

    private readonly string _grid;

    /// <summary>The grid of a document's view and its tempo map.</summary>
    public GridSnap(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _tempo = TempoMap.From(document);
        _signature = document.TimeSignature;
        _grid = document.View.Grid;
    }

    /// <summary>The line nearest to <paramref name="seconds"/>, never before the start.</summary>
    public double Snap(double seconds)
    {
        if (GridRules.Beats(_grid, _signature) is { } step && step > 0)
        {
            double beats = Math.Round(_tempo.BeatsAt(seconds) / step) * step;
            return Math.Max(0, _tempo.TimeAtBeat(beats));
        }
        double length = Step(seconds);
        return length > 0 ? Math.Round(seconds / length) * length : seconds;
    }

    /// <summary>The length of one step, in seconds, at the tempo in force at <paramref name="at"/>.</summary>
    public double Step(double at) => GridRules.StepSeconds(_grid, _tempo, _signature, at);
}
