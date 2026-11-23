namespace AudioLab.Web.Interop;

/// <summary>Reads and sets what the page cannot know about a scrolling element: where it is, how far it is scrolled, and the focus in it.</summary>
public interface IElementView
{
    /// <summary>The left, top, width and height of the element with this id in client pixels; null when there is no such element.</summary>
    public (double Left, double Top, double Width, double Height)? Bounds(string elementId);

    /// <summary>How far the element is scrolled from the left and from the top, in pixels; null when there is no such element.</summary>
    public (double Left, double Top)? Scrolled(string elementId);

    /// <summary>Scrolls the element to an offset from its left and top; nothing happens when there is no such element.</summary>
    public void ScrollTo(string elementId, double left, double top);

    /// <summary>Whether the focus is on the element with this id or inside it.</summary>
    public bool HasFocusWithin(string elementId);
}
