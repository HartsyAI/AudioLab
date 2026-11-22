using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>A well that holds a set of exclusive choices, the chosen one raised.</summary>
public sealed partial class Segmented : ComponentBase
{
    /// <summary>Whether the well uses the smaller radius, for use inside a panel.</summary>
    [Parameter]
    public bool Compact { get; set; }

    /// <summary>The items.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string CssClass => Compact ? "segmented compact" : "segmented";
}
