using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue counter: one well of cells split by a panel-coloured line. It shows values; it does not edit them yet.</summary>
public sealed partial class Counter
{
    /// <summary>The cells, left to right.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<CounterCell> Cells { get; set; } = [];

    /// <summary>What the counter is, as a screen reader says it.</summary>
    [Parameter]
    public string Label { get; set; } = "Position and tempo";
}
