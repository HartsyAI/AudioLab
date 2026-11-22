using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue toggle, labelled or a track letter (M, S, R, I): off is a raised control, on is filled with its state colour.</summary>
public sealed partial class Toggle
{
    /// <summary>Whether the toggle is on.</summary>
    [Parameter]
    public bool On { get; set; }

    /// <summary>The colour it fills with when on.</summary>
    [Parameter]
    public ToggleTone Tone { get; set; }

    /// <summary>True for the small letter toggle of a track header or a channel strip.</summary>
    [Parameter]
    public bool Track { get; set; }

    /// <summary>The text on the toggle: a word, or a single letter for a track toggle.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>The accessible name; a letter needs it, and a labelled toggle repeats its label.</summary>
    [Parameter, EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the toggle does nothing now.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Why it does nothing now; the toggle then carries a tooltip that says so.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised when the toggle is pressed.</summary>
    [Parameter]
    public EventCallback OnClick { get; set; }

    private string ToneName => Tone.ToString().ToLowerInvariant();

    private string SizeName => Track ? "track" : "labelled";

    private string Pressed => On ? "true" : "false";
}
