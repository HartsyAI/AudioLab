using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue channel fader: a vertical slot, a cap with a centre line, and the decibel scale at its left. It shows the gain; it does not take a gesture yet.</summary>
public sealed partial class Fader
{
    /// <summary>The position along the scale, from 0 at the bottom to 1 at the top.</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>The gain written as decibels, for a screen reader.</summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>What the fader sets, as a screen reader says it.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    private string Percent => (Math.Clamp(Position, 0, 1) * 100).ToString("0.##", CultureInfo.InvariantCulture);
}
