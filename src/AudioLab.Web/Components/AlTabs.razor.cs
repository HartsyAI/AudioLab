using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue tabs: arrow keys move between tabs, and the active one carries the accent underline.</summary>
public sealed partial class AlTabs
{
    /// <summary>The tab names, in order.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<string> Tabs { get; set; } = [];

    /// <summary>The panel for a tab, given its name.</summary>
    [Parameter]
    public RenderFragment<string>? Panel { get; set; }
}
