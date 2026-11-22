using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue button: default, primary, danger or icon. A named button also carries its name and shortcut as its title.</summary>
public sealed partial class Button
{
    /// <summary>Which of the looks the button takes.</summary>
    [Parameter]
    public ButtonVariant Variant { get; set; }

    /// <summary>Whether the button does nothing; a named button stays focusable, and its title says why.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Why the button is disabled, in one sentence; the title shows it in place of the name.</summary>
    [Parameter]
    public string? DisabledReason { get; set; }

    /// <summary>The control's name: its accessible name and its title. Required for an icon button.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>The shortcut the title shows after the name, as the shortcut registry writes it.</summary>
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

    // A button with a name stays focusable while it is disabled, so a keyboard user can read why in its title.
    private bool NativelyDisabled => Disabled && Name is null;

    private string? AriaDisabled => Disabled && Name is not null ? "true" : null;

    private string? Hint
    {
        get
        {
            if (Disabled && DisabledReason is not null)
            {
                return DisabledReason;
            }
            return Name is null ? null : Shortcut is null ? Name : $"{Name} ({Shortcut})";
        }
    }

    private Task ClickAsync()
    {
        return Disabled ? Task.CompletedTask : OnClick.InvokeAsync();
    }
}
