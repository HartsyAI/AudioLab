using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The bottom panel: tabs for the selection's tools, the mixer's strips and the generate panel docked at the right.</summary>
public sealed partial class BottomPanel : ComponentBase
{
    private static readonly string[] _tabs = ["Mixer", "Clip editor", "Piano roll", "Effects", "Instruments", "Stems", "Score", "Lyrics", "Voice agent"];

    /// <summary>The session shown.</summary>
    [Parameter]
    [EditorRequired]
    public SessionModel Session { get; set; } = null!;

    private static string[] Tabs => _tabs;

    /// <inheritdoc />
    protected override bool ShouldRender() => false;
}
