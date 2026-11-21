using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>A dialog built on the native dialog element and C#: the browser supplies the top layer, the inert page and Escape.</summary>
public sealed partial class AlNativeDialog
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    private bool _shown;

    private bool _watching;

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

    [Inject]
    private INativeOverlay Overlay { get; set; } = default!;

    private string DialogId => $"al-native-dialog-{_id}";

    private string TitleId => $"al-native-{_id}";

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (Open && !_shown)
        {
            _shown = true;
            if (!_watching)
            {
                _watching = true;
                Overlay.OnClosed(DialogId, () => _ = InvokeAsync(ClosedAsync));
            }
            Overlay.ShowModal(DialogId);
        }
        else if (!Open && _shown)
        {
            _shown = false;
            Overlay.Close(DialogId);
        }
    }

    private async Task ClosedAsync()
    {
        _shown = false;
        await OpenChanged.InvokeAsync(false);
    }

    private Task RequestCloseAsync()
    {
        return OpenChanged.InvokeAsync(false);
    }

    private async Task ConfirmAsync()
    {
        await OnConfirm.InvokeAsync();
        await OpenChanged.InvokeAsync(false);
    }
}
