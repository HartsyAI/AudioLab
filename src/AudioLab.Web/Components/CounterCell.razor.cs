using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>One cell of a <see cref="Counter"/>: a micro caption over its value.</summary>
public sealed partial class CounterCell : ComponentBase
{
    /// <summary>The caption, in capitals.</summary>
    [Parameter]
    [EditorRequired]
    public string Caption { get; set; } = string.Empty;

    /// <summary>The value shown.</summary>
    [Parameter]
    [EditorRequired]
    public string Value { get; set; } = string.Empty;

    /// <summary>True for the readout-size cells (time and bars); false for the body-size ones.</summary>
    [Parameter]
    public bool Large { get; set; }

    private string CssClass => Large ? "cell large" : "cell";
}
