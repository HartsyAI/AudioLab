using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue effect slot: a recessed slot that names its effect, or reads "Add effect"; a button when it opens the chain.</summary>
public sealed partial class EffectSlot
{
    /// <summary>The effect's name, or null for an empty slot.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Whether the effect is in the signal path; a bypassed effect is dimmed and says "off".</summary>
    [Parameter]
    public bool Enabled { get; set; } = true;

    /// <summary>What a screen reader says for the slot, such as "Reverb, on"; the name when absent.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>The slot's title: what it is and what pressing it does, with the shortcut.</summary>
    [Parameter]
    public string? Hint { get; set; }

    /// <summary>Raised when the slot is pressed; with it the slot is a button.</summary>
    [Parameter]
    public EventCallback OnOpen { get; set; }

    private Task OpenAsync() => OnOpen.InvokeAsync();
}
