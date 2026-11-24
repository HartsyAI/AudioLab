using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue progress, attached to what is busy: an accent bar in a well for a known share, a spinner for an unknown one.</summary>
public sealed partial class Progress
{
    /// <summary>What is busy, as a screen reader says it, such as "Uploading kick.wav".</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>The share done, from 0 to 1, or null when there is no figure and the control is a spinner.</summary>
    [Parameter]
    public double? Fraction { get; set; }

    /// <summary>What it says beside the bar or the spinner: a percentage, or the time elapsed.</summary>
    [Parameter]
    public string Text { get; set; } = string.Empty;

    /// <summary>Raised when the cancel control is pressed; without a handler there is no cancel control.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>The cancel control's name; it defaults to the label with "Cancel" in front.</summary>
    [Parameter]
    public string? CancelLabel { get; set; }

    private string CancelName => CancelLabel ?? $"Cancel: {Label}";

    private string? ValueNow => Fraction is double fraction ? Css.Number(Math.Clamp(fraction, 0, 1) * 100) : null;
}
