using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>Calls the dialog methods through [JSImport] on browser built-ins, so no script file is needed.</summary>
public sealed partial class NativeOverlay : INativeOverlay
{
    private readonly List<string> _hosts = [];

    /// <inheritdoc />
    public void ShowModal(string dialogId)
    {
        ShowModalOn(ById(dialogId));
        foreach (string host in _hosts)
        {
            Lift(host);
        }
    }

    /// <inheritdoc />
    public void Close(string dialogId)
    {
        CloseOn(ById(dialogId));
    }

    /// <inheritdoc />
    public void OnClosed(string dialogId, Action closed)
    {
        AddListener(ById(dialogId), "close", closed);
    }

    /// <inheritdoc />
    public void KeepOpenWhileInnerOverlayIsExpanded(string dialogId)
    {
        string selector = $"#{dialogId} [aria-expanded=\"true\"]";
        JSObject dialog = ById(dialogId);
        bool expandedAtKeydown = false;
        AddCapturingListener(dialog, "keydown", _ => expandedAtKeydown = QuerySelector(selector) is not null, true);
        AddCancelListener(dialog, "cancel", cancel =>
        {
            if (expandedAtKeydown)
            {
                PreventDefault(cancel);
            }
        });
    }

    /// <inheritdoc />
    public void KeepAboveModals(string popoverId)
    {
        if (!_hosts.Contains(popoverId))
        {
            _hosts.Add(popoverId);
        }
        Lift(popoverId);
    }

    private static void Lift(string popoverId)
    {
        JSObject host = ById(popoverId);
        HidePopoverOn(host);
        ShowPopoverOn(host);
    }

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddCancelListener(JSObject target, string type, [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> callback);

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddCapturingListener(
        JSObject target,
        string type,
        [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> callback,
        bool capture);

    [JSImport("globalThis.document.querySelector")]
    private static partial JSObject? QuerySelector(string selector);

    [JSImport("globalThis.Event.prototype.preventDefault.call")]
    private static partial void PreventDefault(JSObject cancel);

    [JSImport("globalThis.HTMLElement.prototype.showPopover.call")]
    private static partial void ShowPopoverOn(JSObject element);

    [JSImport("globalThis.HTMLElement.prototype.hidePopover.call")]
    private static partial void HidePopoverOn(JSObject element);

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddListener(JSObject target, string type, [JSMarshalAs<JSType.Function>] Action callback);

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject ById(string id);

    [JSImport("globalThis.HTMLDialogElement.prototype.showModal.call")]
    private static partial void ShowModalOn(JSObject dialog);

    [JSImport("globalThis.HTMLDialogElement.prototype.close.call")]
    private static partial void CloseOn(JSObject dialog);
}
