using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue segmented control: a well that holds exclusive choices, the chosen one raised. The transport variant has raised items instead.</summary>
public sealed partial class Segmented
{
    /// <summary>The choices.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<SegmentItem> Items { get; set; } = [];

    /// <summary>The id of the chosen item, or null for none.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>The id of the item drawn wider than the others, such as play in the transport.</summary>
    [Parameter]
    public string? Wide { get; set; }

    /// <summary>True for the transport: every item is a raised control.</summary>
    [Parameter]
    public bool Transport { get; set; }

    /// <summary>What the group is for, as a screen reader says it.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Raised with the id of the item chosen.</summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    private Task ChooseAsync(SegmentItem item)
    {
        return ValueChanged.InvokeAsync(item.Id);
    }
}
