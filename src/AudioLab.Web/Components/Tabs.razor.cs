using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue tabs of the bottom panel: the arrow keys move between tabs, the active tab is strong with a two-pixel accent underline.</summary>
public sealed partial class Tabs
{
    /// <summary>The tab names, in order.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<string> Names { get; set; } = [];

    /// <summary>What the tabs are for, as a screen reader says it.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>The panel for a tab, given its name.</summary>
    [Parameter]
    public RenderFragment<string>? TabContent { get; set; }
}
