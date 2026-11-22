using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>An inline icon from the one set, in the current text colour. It is decorative: the control that holds it carries the name.</summary>
public sealed partial class Icon
{
    /// <summary>Which icon to draw.</summary>
    [Parameter, EditorRequired]
    public IconName Name { get; set; }

    /// <summary>The width and height in pixels.</summary>
    [Parameter]
    public int Size { get; set; } = 14;

    private string Path => IconPaths.Of(Name);
}
