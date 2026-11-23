using AudioLab.Core.Rules;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The keys at the left of the piano roll, one row for each pitch, the highest at the top; a press on a key can be heard.</summary>
public sealed partial class PianoKeys
{
    private string? _shown = "";

    private bool _drawn;

    /// <summary>The scale; only the row height matters here.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>Why a key cannot be heard now, which the keys show as their title; null when it can.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised with the press when the primary button goes down on the keys.</summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnPress { get; set; }

    /// <summary>Raised when the pointer is let go on the keys or leaves them.</summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnRelease { get; set; }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        bool draw = !_drawn || _shown != Reason;
        _drawn = true;
        _shown = Reason;
        return draw;
    }

    private static string Label(int pitch) => pitch % 12 == 0 ? NoteRules.Name(pitch) : string.Empty;
}
