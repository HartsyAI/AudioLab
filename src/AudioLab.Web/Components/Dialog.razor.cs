using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue dialog, confirm or form: the native dialog element driven from C#; the browser supplies the top layer and Escape.</summary>
public sealed partial class Dialog
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    private bool _shown;

    private bool _watching;

    private DialogContext _context = new(string.Empty, false);

    /// <summary>Whether the dialog is open.</summary>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Raised with the new state when the dialog opens or closes.</summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    /// <summary>The heading.</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>One sentence that says what the action does; a form dialog may leave it out.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>The confirm button's label: the action it takes, such as "Delete project", never "OK".</summary>
    [Parameter, EditorRequired]
    public string ConfirmText { get; set; } = string.Empty;

    /// <summary>The look of the confirm button: danger for a deletion, primary otherwise.</summary>
    [Parameter]
    public ButtonVariant ConfirmVariant { get; set; } = ButtonVariant.Primary;

    /// <summary>Whether the confirm button does nothing yet, such as a form with a missing value.</summary>
    [Parameter]
    public bool ConfirmDisabled { get; set; }

    /// <summary>Why the confirm button does nothing yet, in one sentence; it shows beside the buttons and as the button's title.</summary>
    [Parameter]
    public string? ConfirmDisabledReason { get; set; }

    /// <summary>Fields of a form dialog, placed between the text and the buttons.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Why the last confirm failed, in one line; it shows above the buttons until the parent clears it.</summary>
    [Parameter]
    public string? Error { get; set; }

    /// <summary>Raised when the user confirms, by the button or by Enter in a field; the parent closes the dialog when it succeeded.</summary>
    [Parameter]
    public EventCallback OnConfirm { get; set; }

    [Inject]
    private INativeOverlay Overlay { get; set; } = default!;

    private string DialogId => $"dialog-{_id}";

    private string TitleId => $"dialog-title-{_id}";

    private string TextId => $"dialog-text-{_id}";

    private string ErrorId => $"dialog-error-{_id}";

    private string? DescribedBy
    {
        get
        {
            string[] ids = [.. new[] { Description is null ? null : TextId, Error is null ? null : ErrorId }.OfType<string>()];
            return ids.Length == 0 ? null : string.Join(' ', ids);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _context = new DialogContext(ErrorId, Error is not null);
    }

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
                Overlay.KeepOpenWhileInnerOverlayIsExpanded(DialogId);
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
        if (Open)
        {
            await OpenChanged.InvokeAsync(false);
        }
    }

    private Task RequestCloseAsync()
    {
        return OpenChanged.InvokeAsync(false);
    }

    private async Task ConfirmAsync()
    {
        if (ConfirmDisabled)
        {
            return;
        }
        await OnConfirm.InvokeAsync();
    }
}
