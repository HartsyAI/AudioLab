using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue knob: a raised disc with a dark edge and a pointer, its value beside it. It shows a value; it does not take a gesture yet.</summary>
public sealed partial class Knob
{
    private const double SweepDegrees = 135;

    /// <summary>The setting from -1 (full left) through 0 (centre) to 1 (full right).</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>The value as written beside the knob.</summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>What the knob sets, as a screen reader says it.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    private string Transform => $"rotate({(Math.Clamp(Position, -1, 1) * SweepDegrees).ToString("0.#", CultureInfo.InvariantCulture)} 12 12)";
}
