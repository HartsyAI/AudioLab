using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue dialog: modal, traps focus, closes on Escape, and names its action on the confirm button.</summary>
public sealed partial class AlDialog
{
    /// <summary>Whether the dialog is open.</summary>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Raised with the new state when the dialog opens or closes.</summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>The heading, which also names the dialog for assistive technology.</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>One sentence that says what the action does.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>The confirm button's label: the action itself ("Delete project"), never "OK".</summary>
    [Parameter, EditorRequired]
    public string ConfirmText { get; set; } = string.Empty;

    /// <summary>The cancel button's label.</summary>
    [Parameter]
    public string CancelText { get; set; } = "Cancel";

    /// <summary>The confirm button's look; danger for a destructive action.</summary>
    [Parameter]
    public AlButtonVariant ConfirmVariant { get; set; } = AlButtonVariant.Primary;

    /// <summary>Raised when the user confirms; the dialog closes afterwards.</summary>
    [Parameter]
    public EventCallback OnConfirm { get; set; }

    /// <summary>Fields for a form dialog, placed between the text and the buttons.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private async Task CloseAsync()
    {
        await OpenChanged.InvokeAsync(false);
    }

    private async Task ConfirmAsync()
    {
        await OnConfirm.InvokeAsync();
        await CloseAsync();
    }
}
