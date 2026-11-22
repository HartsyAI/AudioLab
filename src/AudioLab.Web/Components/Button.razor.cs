using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue button: default, primary, danger or icon; with a name it also carries the tooltip of its name and shortcut.</summary>
public sealed partial class Button
{
    /// <summary>Which of the looks the button takes.</summary>
    [Parameter]
    public ButtonVariant Variant { get; set; }

    /// <summary>Whether the button does nothing; with a name, the tooltip says why.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Why the button is disabled, in one sentence; the tooltip shows it in place of the name.</summary>
    [Parameter]
    public string? DisabledReason { get; set; }

    /// <summary>The control's name: its accessible name and the first line of its tooltip. Required for an icon button.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>The shortcut the tooltip shows, as the shortcut registry writes it.</summary>
    [Parameter]
    public string? Shortcut { get; set; }

    /// <summary>True for the button that submits its form, such as the confirm button of a dialog.</summary>
    [Parameter]
    public bool Submit { get; set; }

    /// <summary>An address to go to; the control is then a link that looks like a button.</summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>Raised when the button is activated.</summary>
    [Parameter]
    public EventCallback OnClick { get; set; }

    /// <summary>The label or the icon.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Extra attributes, such as a data attribute, forwarded to the element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? Attributes { get; set; }

    private string VariantName => Variant.ToString().ToLowerInvariant();

    private string ButtonType => Submit ? "submit" : "button";

    private string? AriaDisabled => Disabled ? "true" : null;

    private string TooltipText => Disabled && DisabledReason is not null ? DisabledReason : Name ?? string.Empty;

    private Task ClickAsync()
    {
        return Disabled ? Task.CompletedTask : OnClick.InvokeAsync();
    }
}
