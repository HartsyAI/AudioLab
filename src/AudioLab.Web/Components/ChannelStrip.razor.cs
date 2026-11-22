using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>One mixer strip: name, effect slots, pan, fader with scale and meter, mute and solo, and the level readout.</summary>
public sealed partial class ChannelStrip : ComponentBase
{
    /// <summary>The strip shown.</summary>
    [Parameter]
    [EditorRequired]
    public StripModel Strip { get; set; } = null!;

    private string CssClass => Strip.Master ? "strip master" : "strip";

    /// <inheritdoc />
    protected override bool ShouldRender() => false;
}
