using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue text field: a recessed well with its label above it.</summary>
public sealed partial class Field
{
    /// <summary>The text.</summary>
    [Parameter]
    public string Value { get; set; } = string.Empty;

    /// <summary>Raised with the new text on every keystroke.</summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>What the field is for; also its accessible name.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>What the empty field shows.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>The most characters the field accepts.</summary>
    [Parameter]
    public int MaxLength { get; set; } = 200;

    /// <summary>Whether the value was refused; a field inside a dialog that shows a failure is marked as well.</summary>
    [Parameter]
    public bool Invalid { get; set; }

    /// <summary>Whether the field takes several lines, as a text area, with the same surface and focus ring.</summary>
    [Parameter]
    public bool Multiline { get; set; }

    /// <summary>Extra attributes forwarded to the input, such as an id.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? Attributes { get; set; }

    [CascadingParameter]
    private DialogContext? Dialog { get; set; }

    private string? AriaInvalid => Invalid || Dialog is { HasError: true } ? "true" : null;

    private string? DescribedBy => Dialog is { HasError: true } ? Dialog.ErrorId : null;

    private Task ChangedAsync(ChangeEventArgs change)
    {
        return ValueChanged.InvokeAsync(change.Value as string ?? string.Empty);
    }
}
