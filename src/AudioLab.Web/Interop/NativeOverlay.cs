using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>Calls the dialog methods through [JSImport] on browser built-ins, so no script file is needed.</summary>
public sealed partial class NativeOverlay : INativeOverlay
{
    /// <inheritdoc />
    public void ShowModal(string dialogId)
    {
        ShowModalOn(ById(dialogId));
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

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddListener(JSObject target, string type, [JSMarshalAs<JSType.Function>] Action callback);

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject ById(string id);

    [JSImport("globalThis.HTMLDialogElement.prototype.showModal.call")]
    private static partial void ShowModalOn(JSObject dialog);

    [JSImport("globalThis.HTMLDialogElement.prototype.close.call")]
    private static partial void CloseOn(JSObject dialog);
}
