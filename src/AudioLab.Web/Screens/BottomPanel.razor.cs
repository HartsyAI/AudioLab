using AudioLab.Core.Model;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The bottom panel: tabs for the selection's tools. Only the mixer has content yet; every other tab says so.</summary>
public sealed partial class BottomPanel
{
    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>The selected track, or null.</summary>
    [Parameter]
    public TrackId? Selected { get; set; }

    private static IReadOnlyList<string> Names { get; } =
        ["Mixer", "Clip editor", "Piano roll", "Effects", "Instruments", "Stems", "Score", "Lyrics", "Voice agent"];
}
