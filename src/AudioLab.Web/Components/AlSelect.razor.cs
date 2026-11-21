using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue select: a closed button showing the value, a listbox of choices when open.</summary>
public sealed partial class AlSelect
{
    /// <summary>The chosen option, or null for none.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Raised when the user chooses an option.</summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>The choices.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<string> Options { get; set; } = [];

    /// <summary>The accessible name of the control.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>What the closed control shows before a choice is made.</summary>
    [Parameter]
    public string Placeholder { get; set; } = "Choose";
}
