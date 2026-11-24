namespace AudioLab.Web.Interop;

/// <summary>Moves the keyboard focus to an element by its id.</summary>
public interface IElementFocus
{
    /// <summary>Focuses the element with this id, scrolling it into view; nothing happens when there is none.</summary>
    public void Focus(string elementId);

    /// <summary>Focuses the first element matching <paramref name="selector"/> inside the element with this id; nothing happens when there is none.</summary>
    public void FocusIn(string containerId, string selector);
}
