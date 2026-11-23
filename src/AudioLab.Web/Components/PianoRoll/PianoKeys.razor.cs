using AudioLab.Core.Rules;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The keys at the left of the piano roll, one row for each pitch, the highest at the top; the octave of a C is written on it.</summary>
public sealed partial class PianoKeys
{
    private bool _drawn;

    /// <summary>The scale; only the row height matters here.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        bool draw = !_drawn;
        _drawn = true;
        return draw;
    }

    private static string Label(int pitch) => pitch % 12 == 0 ? NoteRules.Name(pitch) : string.Empty;
}
