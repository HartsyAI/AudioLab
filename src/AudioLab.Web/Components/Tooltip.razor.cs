using Bit.Bheadless;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue tooltip around one button: its name, then its shortcut. The button is the trigger, so keyboard focus shows it.</summary>
/// <remarks>The button's look comes from the component that uses this one, through <c>class</c> and the other attributes it passes.</remarks>
public sealed partial class Tooltip
{
    /// <summary>What the tooltip says first when that is not the control's name, such as why the control is disabled.</summary>
    [Parameter]
    public string? Text { get; set; }

    /// <summary>The control's accessible name.</summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>The shortcut as the shortcut registry writes it; shown after the text, disabled or not, as the menu shows it.</summary>
    [Parameter]
    public string? Shortcut { get; set; }

    /// <summary>Whether the control does nothing now; it stays focusable, becomes a plain button so it submits nothing, and the text says why.</summary>
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

    // The library places the tooltip from pixels, and it ignores margins (measured), so the gap cannot be a token in CSS: this is
    // --space-6 (0.375rem) at the 16 pixel root. It keeps the tooltip off the control it describes.
    private const int GapPixels = 6;

    private static BitBhAnchorOptions Anchor { get; } = new() { To = "bottom", Gap = GapPixels };

    private Task ClickAsync()
    {
        return Activation.RunAsync(Disabled, OnClick);
    }
}
