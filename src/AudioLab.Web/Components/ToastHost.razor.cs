using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>Where toasts appear: one per screen, a labelled region, kept in the top layer above any modal dialog.</summary>
public sealed partial class ToastHost
{
    private const string HostId = "toast-host";

    private bool _lifted;

    [Inject]
    private INativeOverlay Overlay { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Toasts.Changed += Refresh;
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (!_lifted)
        {
            _lifted = true;
            Overlay.KeepAboveModals(HostId);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Toasts.Changed -= Refresh;
    }

    private static string VariantOf(ToastItem toast) => toast.Kind.ToString().ToLowerInvariant();

    // An error interrupts a screen reader; the others wait their turn.
    private static string RoleOf(ToastItem toast) => toast.Kind == ToastKind.Error ? "alert" : "status";

    private void Refresh()
    {
        _ = InvokeAsync(StateHasChanged);
    }
}
