using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>Where toasts appear: one per screen, a labelled region that F8 jumps to, kept in the top layer above any modal dialog.</summary>
public sealed partial class ToastHost
{
    private const string HostId = "toast-host";

    private bool _lifted;

    [Inject]
    private INativeOverlay Overlay { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (!_lifted)
        {
            _lifted = true;
            Overlay.KeepAboveModals(HostId);
        }
    }
}
