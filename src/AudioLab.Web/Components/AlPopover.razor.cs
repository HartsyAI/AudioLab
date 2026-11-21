using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>A floating panel that opens from a button and leaves the rest of the screen usable.</summary>
public sealed partial class AlPopover
{
    /// <summary>The button's text.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>The panel's content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
