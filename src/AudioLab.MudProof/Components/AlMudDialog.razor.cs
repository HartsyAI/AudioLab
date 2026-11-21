using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AudioLab.MudProof.Components;

/// <summary>A catalogue dialog on MudBlazor's dialog, to see what it takes to keep the look ours.</summary>
public sealed partial class AlMudDialog
{
    private readonly DialogOptions _options = new() { CloseOnEscapeKey = true, BackdropClick = false };

    /// <summary>Whether the dialog is open.</summary>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Raised with the new state when the dialog opens or closes.</summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>The heading.</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>One sentence that says what the action does.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>The confirm button's label.</summary>
    [Parameter, EditorRequired]
    public string ConfirmText { get; set; } = string.Empty;

    /// <summary>Raised when the user confirms.</summary>
    [Parameter]
    public EventCallback OnConfirm { get; set; }

    private Task CloseAsync()
    {
        return OpenChanged.InvokeAsync(false);
    }

    private async Task ConfirmAsync()
    {
        await OnConfirm.InvokeAsync();
        await OpenChanged.InvokeAsync(false);
    }
}
