using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>A one-letter track toggle (M, S, R, I) that fills with its state colour when on.</summary>
public sealed partial class TrackToggle : ComponentBase
{
    /// <summary>The letter shown.</summary>
    [Parameter]
    [EditorRequired]
    public string Letter { get; set; } = string.Empty;

    /// <summary>Accessible name, for example Mute.</summary>
    [Parameter]
    [EditorRequired]
    public string AriaLabel { get; set; } = string.Empty;

    /// <summary>The colour the toggle takes when on.</summary>
    [Parameter]
    public ToggleTone Tone { get; set; }

    /// <summary>Whether the toggle is engaged.</summary>
    [Parameter]
    public bool On { get; set; }

    private string CssClass => On ? $"toggle on {Tone.ToString().ToLowerInvariant()}" : "toggle";
}
