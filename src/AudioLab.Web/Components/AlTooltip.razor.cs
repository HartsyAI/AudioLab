using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue tooltip: the name of a control, then its shortcut, after a pause and never over the control.</summary>
public sealed partial class AlTooltip
{
    /// <summary>What the control is called.</summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>The control's keyboard shortcut, as the shortcut registry writes it.</summary>
    [Parameter]
    public string? Shortcut { get; set; }

    /// <summary>True when the trigger is itself the focusable button; false wraps the control in a span. A span does not see keyboard focus.</summary>
    [Parameter]
    public bool AsButton { get; set; }

    /// <summary>The control the tooltip describes.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
