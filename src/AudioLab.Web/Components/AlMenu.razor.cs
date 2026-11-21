using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue dropdown menu: a button that opens commands, destructive ones last.</summary>
public sealed partial class AlMenu
{
    /// <summary>The button's text.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>The commands, each with its shortcut.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<AlMenuItem> Items { get; set; } = [];

    /// <summary>Raised with the chosen command.</summary>
    [Parameter]
    public EventCallback<AlMenuItem> OnSelect { get; set; }

    private IEnumerable<AlMenuItem> Ordered => Items.OrderBy(item => item.Destructive);

    private Task SelectAsync(AlMenuItem item)
    {
        return OnSelect.InvokeAsync(item);
    }
}
