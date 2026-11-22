using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue panel: a titled region of the panel surface, split from its neighbours by a dark line; no shadow, no nesting.</summary>
public sealed partial class Panel
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    /// <summary>The panel's title.</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Controls at the right of the title bar, such as the panel's main action.</summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>What the panel holds.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string TitleId => $"panel-title-{_id}";
}
