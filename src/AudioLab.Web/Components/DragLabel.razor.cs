using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue drag label: the name of what is dragged, with what it is or why it cannot land; it takes no pointer events.</summary>
public sealed partial class DragLabel
{
    /// <summary>What is dragged.</summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>What it is, in label size, such as its length; shown when there is no reason.</summary>
    [Parameter]
    public string? Detail { get; set; }

    /// <summary>Why it cannot land where the pointer is; when set it replaces the detail.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>The left edge in pixels from the left of the window.</summary>
    [Parameter]
    public double Left { get; set; }

    /// <summary>The top edge in pixels from the top of the window.</summary>
    [Parameter]
    public double Top { get; set; }
}
