using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue empty state: a heading, one sentence and the one to three actions that get the user moving. No illustration.</summary>
public sealed partial class EmptyState
{
    /// <summary>What is empty, or what went wrong.</summary>
    [Parameter, EditorRequired]
    public string Heading { get; set; } = string.Empty;

    /// <summary>One sentence that says what to do about it.</summary>
    [Parameter, EditorRequired]
    public string Message { get; set; } = string.Empty;

    /// <summary>The actions, as buttons.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
