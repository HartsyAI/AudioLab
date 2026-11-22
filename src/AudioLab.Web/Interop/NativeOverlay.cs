using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Calls the dialog methods through [JSImport] on browser built-ins, so no script file is needed.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class NativeOverlay : INativeOverlay
{
    private readonly List<string> _hosts = [];

    // WebKit returns focus to the page body after a native dialog opened with a mouse click, so the opener is put back by hand.
    private readonly Dictionary<string, JSObject?> _openers = [];

    /// <inheritdoc />
    public void ShowModal(string dialogId)
    {
        if (_openers.Remove(dialogId, out JSObject? previous))
        {
            previous?.Dispose();
        }
        _openers[dialogId] = QuerySelector(":focus");
        using JSObject dialog = ById(dialogId);
        ShowModalOn(dialog);
        foreach (string host in _hosts)
        {
            Lift(host);
        }
    }

    /// <inheritdoc />
    public void Close(string dialogId)
    {
        using JSObject dialog = ById(dialogId);
        CloseOn(dialog);
    }

    /// <inheritdoc />
    public void OnClosed(string dialogId, Action closed)
    {
        using JSObject dialog = ById(dialogId);
        AddListener(dialog, "close", () =>
        {
            if (_openers.Remove(dialogId, out JSObject? opener) && opener is not null)
            {
                FocusOn(opener);
                opener.Dispose();
            }
            closed();
        });
    }

    /// <inheritdoc />
    public void KeepOpenWhileInnerOverlayIsExpanded(string dialogId)
    {
        string selector = $"#{dialogId} [aria-expanded=\"true\"]";
        using JSObject dialog = ById(dialogId);
        bool expandedAtKeydown = false;
        AddCapturingListener(dialog, "keydown", keydown =>
        {
            keydown.Dispose();
            using JSObject? expanded = QuerySelector(selector);
            expandedAtKeydown = expanded is not null;
        }, true);
        AddCancelListener(dialog, "cancel", cancel =>
        {
            if (expandedAtKeydown)
            {
                PreventDefault(cancel);
            }
            cancel.Dispose();
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
        using JSObject host = ById(popoverId);
        HidePopoverOn(host);
        ShowPopoverOn(host);
    }

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddCancelListener(
        JSObject target,
        string type,
        [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> callback);

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

    [JSImport("globalThis.HTMLElement.prototype.focus.call")]
    private static partial void FocusOn(JSObject element);

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
