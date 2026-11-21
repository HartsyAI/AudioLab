using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue context menu: right-click, or the menu key, on the content opens commands, destructive ones last.</summary>
public sealed partial class AlContextMenu
{
    /// <summary>The commands, each with its shortcut.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<AlMenuItem> Items { get; set; } = [];

    /// <summary>Raised with the chosen command.</summary>
    [Parameter]
    public EventCallback<AlMenuItem> OnSelect { get; set; }

    /// <summary>The area the menu belongs to.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private IEnumerable<AlMenuItem> Ordered => Items.OrderBy(item => item.Destructive);

    private Task SelectAsync(AlMenuItem item)
    {
        return OnSelect.InvokeAsync(item);
    }
}
