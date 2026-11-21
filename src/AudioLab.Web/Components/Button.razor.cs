using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue's button: raised by default, with primary, go, chosen, bare and glyph looks.</summary>
public sealed partial class Button : ComponentBase
{
    /// <summary>Which look the button has.</summary>
    [Parameter]
    public ButtonKind Kind { get; set; } = ButtonKind.Default;

    /// <summary>Which size step the button uses.</summary>
    [Parameter]
    public ButtonSize Size { get; set; } = ButtonSize.Medium;

    /// <summary>Fixed width in pixels, for the items of a segmented control.</summary>
    [Parameter]
    public int? Width { get; set; }

    /// <summary>Fixed height in pixels, for the items of a segmented control.</summary>
    [Parameter]
    public int? Height { get; set; }

    /// <summary>Accessible name of an icon-only button.</summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>Glyph or label.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string CssClass => $"button {Kind.ToString().ToLowerInvariant()} {Size.ToString().ToLowerInvariant()}";

    private string? InlineSize => (Width, Height) switch
    {
        (int w, int h) => $"width: {w}px; height: {h}px; padding: 0",
        (int w, null) => $"width: {w}px; padding: 0",
        (null, int h) => $"height: {h}px",
        _ => null,
    };
}
