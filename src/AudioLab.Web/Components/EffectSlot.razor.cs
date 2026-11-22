using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue effect slot: a recessed slot that names its effect, or reads "Add effect" when empty. It is a display until the effects screen exists.</summary>
public sealed partial class EffectSlot
{
    /// <summary>The effect's name, or null for an empty slot.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Whether the effect is in the signal path; a bypassed effect is dimmed.</summary>
    [Parameter]
    public bool Enabled { get; set; } = true;
}
