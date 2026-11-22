using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>One track's header: colour strip, number, name with its toggles, source with volume, and the meter.</summary>
public sealed partial class TrackHeader : ComponentBase
{
    /// <summary>The track shown.</summary>
    [Parameter]
    [EditorRequired]
    public TrackModel Track { get; set; } = null!;

    private string CssClass => Track.Selected ? "header selected" : "header";

    /// <inheritdoc />
    protected override bool ShouldRender() => false;
}
