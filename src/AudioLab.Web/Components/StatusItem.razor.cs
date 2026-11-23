using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue status item: label-sized text in the faint colour, with a six-pixel dot when it states a state.</summary>
public sealed partial class StatusItem
{
    /// <summary>The state dot, or null for plain text.</summary>
    [Parameter]
    public StatusDot? Dot { get; set; }

    /// <summary>Whether the text is the brighter secondary colour, as the state word beside a dot is.</summary>
    [Parameter]
    public bool Soft { get; set; }

    /// <summary>The text.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
