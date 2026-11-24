namespace AudioLab.Web.Interop;

/// <summary>Activates an element as if it were clicked, which is how a button opens the browser's file picker.</summary>
public interface IElementClick
{
    /// <summary>Clicks the element with this id; nothing happens when there is none.</summary>
    public void Click(string elementId);
}
