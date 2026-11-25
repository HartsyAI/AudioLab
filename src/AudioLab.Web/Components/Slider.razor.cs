using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue slider: a slot filled to the value, a cap on it and the value beside it; it takes no gesture yet.</summary>
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

    /// <summary>The unit said after the value, such as dB; empty for a value with none.</summary>
    [Parameter]
    public string Unit { get; set; } = "dB";

    private string Percent => (Math.Clamp(Position, 0, 1) * 100).ToString("0.##", CultureInfo.InvariantCulture);

    private string AnnouncedText => Unit.Length == 0 ? $"{Label} {Text}" : $"{Label} {Text} {Unit}";
}
