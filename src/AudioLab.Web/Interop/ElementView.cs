using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Reads and sets scroll and focus state through <c>getBoundingClientRect</c>, <c>scrollTo</c> and <c>contains</c>, built-ins.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class ElementView : IElementView
{
    /// <inheritdoc />
    public (double Left, double Top, double Width, double Height)? Bounds(string elementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        using JSObject? element = ById(elementId);
        if (element is null)
        {
            return null;
        }
        using JSObject rect = Rect(element);
        return (
            rect.GetPropertyAsDouble("left"),
            rect.GetPropertyAsDouble("top"),
            rect.GetPropertyAsDouble("width"),
            rect.GetPropertyAsDouble("height"));
    }

    /// <inheritdoc />
    public (double Left, double Top)? Scrolled(string elementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        using JSObject? element = ById(elementId);
        return element is null ? null : (element.GetPropertyAsDouble("scrollLeft"), element.GetPropertyAsDouble("scrollTop"));
    }

    /// <inheritdoc />
    public void ScrollTo(string elementId, double left, double top)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        using JSObject? element = ById(elementId);
        if (element is not null)
        {
            Scroll(element, left, top);
        }
    }

    /// <inheritdoc />
    public bool HasFocusWithin(string elementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        using JSObject? element = ById(elementId);
        using JSObject? active = Active();
        return element is not null && active is not null && Contains(element, active);
    }

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? ById(string id);

    [JSImport("globalThis.Element.prototype.getBoundingClientRect.call")]
    private static partial JSObject Rect(JSObject element);

    [JSImport("globalThis.Element.prototype.scrollTo.call")]
    private static partial void Scroll(JSObject element, double left, double top);

    [JSImport("globalThis.Node.prototype.contains.call")]
    private static partial bool Contains(JSObject element, JSObject other);

    private static JSObject? Active()
    {
        using JSObject document = JSHost.GlobalThis.GetPropertyAsJSObject("document")!;
        return document.GetPropertyAsJSObject("activeElement");
    }
}
