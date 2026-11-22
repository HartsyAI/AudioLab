using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue dropdown menu: a button that opens commands, each with its shortcut, destructive ones last.</summary>
public sealed partial class Menu
{
    /// <summary>The button's text, and its accessible name when the button shows only an icon.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>True for a square button with three dots in place of the label.</summary>
    [Parameter]
    public bool Icon { get; set; }

    /// <summary>The commands.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<MenuItem> Items { get; set; } = [];

    /// <summary>Raised with the chosen command.</summary>
    [Parameter]
    public EventCallback<MenuItem> OnSelect { get; set; }

    private IEnumerable<MenuItem> Ordered => Items.OrderBy(item => item.Destructive);

    private Task SelectAsync(MenuItem item)
    {
        return OnSelect.InvokeAsync(item);
    }
}
