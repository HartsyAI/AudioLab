namespace AudioLab.Web.Interop;

/// <summary>Measures an element, which the page cannot know from the document alone.</summary>
public interface IElementMetrics
{
    /// <summary>The left and top of the element with this id in client pixels, scroll included; null when there is no such element.</summary>
    public (double Left, double Top)? Origin(string elementId);

    /// <summary>Whether the topmost thing at a client point is the element with this id or in it, so a header over the lanes is not it.</summary>
    public bool IsOver(string elementId, double clientX, double clientY);
}
