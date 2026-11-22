using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>A button that says what it is in its title: its name and shortcut, or why it does nothing now. It stays focusable while disabled.</summary>
/// <remarks>The look comes from the component that uses this one, through <c>class</c> and the other attributes it passes.</remarks>
public sealed partial class TitledButton
{
    /// <summary>The control's accessible name, and the first words of its title.</summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>Why the control does nothing now, in one sentence; the title shows it in place of the name.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>The shortcut as the shortcut registry writes it.</summary>
    [Parameter]
    public string? Shortcut { get; set; }

    /// <summary>Whether the control does nothing now.</summary>
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

    private string Hint
    {
        get
        {
            if (Disabled && Reason is not null)
            {
                return Reason;
            }
            return Shortcut is null ? Name : $"{Name} ({Shortcut})";
        }
    }

    private Task ClickAsync()
    {
        return Activation.RunAsync(Disabled, OnClick);
    }
}
