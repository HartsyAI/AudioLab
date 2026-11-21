namespace AudioLab.Web.Interop;

/// <summary>Opens and closes native dialog elements through the browser's own methods, with no script file.</summary>
public interface INativeOverlay
{
    /// <summary>Calls showModal on the dialog with this id: top layer, inert page, Escape and focus restore from the browser.</summary>
    public void ShowModal(string dialogId);

    /// <summary>Calls a method when the dialog with this id raises its close event, which does not bubble to Blazor's own handlers.</summary>
    public void OnClosed(string dialogId, Action closed);

    /// <summary>Calls close on the dialog with this id.</summary>
    public void Close(string dialogId);
}
