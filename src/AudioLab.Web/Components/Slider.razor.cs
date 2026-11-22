using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue slider: a recessed slot filled up to the value, a cap on the value and the value written beside it. It shows a value and does not take a gesture yet.</summary>
public sealed partial class Slider
{
    /// <summary>The position along the slot, from 0 to 1.</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>The value as written beside the slider.</summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>What the slider sets, as a screen reader says it.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    private string Percent => (Math.Clamp(Position, 0, 1) * 100).ToString("0.##", CultureInfo.InvariantCulture);
}
