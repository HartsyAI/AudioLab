using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue tooltip around one button: its name, then its shortcut. The button is the trigger, so keyboard focus shows it.</summary>
/// <remarks>The button's look comes from the component that uses this one, through <c>class</c> and the other attributes it passes.</remarks>
public sealed partial class Tooltip
{
    /// <summary>What the tooltip says first: the control's name, or why it is disabled.</summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>The control's accessible name.</summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>The shortcut as the shortcut registry writes it.</summary>
    [Parameter]
    public string? Shortcut { get; set; }

    /// <summary>Whether the control does nothing now; the tooltip then says why and shows no shortcut.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>True for the button that submits its form.</summary>
    [Parameter]
    public bool Submit { get; set; }

    /// <summary>Raised when the button is activated and not disabled.</summary>
    [Parameter]
    public EventCallback OnClick { get; set; }

    /// <summary>The label or the icon.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Attributes forwarded to the button: its class, its data attributes, its pressed state.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? Attributes { get; set; }

    private Task ClickAsync()
    {
        return Disabled ? Task.CompletedTask : OnClick.InvokeAsync();
    }
}
