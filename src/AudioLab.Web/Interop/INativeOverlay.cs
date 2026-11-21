namespace AudioLab.Web.Interop;

/// <summary>Opens and closes native dialog and popover elements through the browser's own methods, with no script file.</summary>
public interface INativeOverlay
{
    /// <summary>Calls showModal on the dialog with this id, then lifts every registered top-layer host above it.</summary>
    public void ShowModal(string dialogId);

    /// <summary>Calls a method when the dialog with this id raises its close event, which does not bubble to Blazor's own handlers.</summary>
    public void OnClosed(string dialogId, Action closed);

    /// <summary>Keeps the dialog open when Escape arrives while a library list inside it is expanded.</summary>
    public void KeepOpenWhileInnerOverlayIsExpanded(string dialogId);

    /// <summary>Calls close on the dialog with this id.</summary>
    public void Close(string dialogId);

    /// <summary>Shows the manual popover with this id in the top layer and lifts it above every modal dialog opened later.</summary>
    public void KeepAboveModals(string popoverId);
}
