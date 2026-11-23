using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The lines under the notes: the black key bands, the octave lines, and the grid steps, beats and bars of the tempo map.</summary>
public sealed partial class RollGridLayer
{
    private (RollLines, RollGeometry)? _shown;

    private string _blackRows = string.Empty;

    private string _octaves = string.Empty;

    private double _rowsFor = -1;

    /// <summary>The scale and the clip's length.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>The vertical lines.</summary>
    [Parameter]
    public RollLines Lines { get; set; }

    private string BlackRows => _blackRows;

    private string Octaves => _octaves;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_rowsFor != Geometry.Length)
        {
            (_blackRows, _octaves) = RollGrid.Rows(Geometry.Length);
            _rowsFor = Geometry.Length;
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        (RollLines, RollGeometry) now = (Lines, Geometry);
        bool same = _shown == now;
        _shown = now;
        return !same;
    }
}
