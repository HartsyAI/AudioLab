using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue button: default, primary or danger.</summary>
public sealed partial class AlButton
{
    /// <summary>Which of the three looks the button takes.</summary>
    [Parameter]
    public AlButtonVariant Variant { get; set; }

    /// <summary>Whether the button ignores the pointer and the keyboard.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Raised when the button is activated.</summary>
    [Parameter]
    public EventCallback OnClick { get; set; }

    /// <summary>The label.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Extra attributes, such as an accessible name, forwarded to the button element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? Attributes { get; set; }

    private string VariantName => Variant.ToString().ToLowerInvariant();
}
