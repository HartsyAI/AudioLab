using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Reads <c>getBoundingClientRect</c> and <c>elementFromPoint</c>, browser built-ins.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class ElementMetrics : IElementMetrics
{
    /// <inheritdoc />
    public (double Left, double Top)? Origin(string elementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        using JSObject? element = ById(elementId);
        if (element is null)
        {
            return null;
        }
        using JSObject rect = Rect(element);
        return (rect.GetPropertyAsDouble("left"), rect.GetPropertyAsDouble("top"));
    }

    /// <inheritdoc />
    public bool IsOver(string elementId, double clientX, double clientY)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        using JSObject? top = AtPoint(clientX, clientY);
        if (top is null)
        {
            return false;
        }
        using JSObject? found = Closest(top, $"#{elementId}");
        return found is not null;
    }

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? ById(string id);

    [JSImport("globalThis.document.elementFromPoint")]
    private static partial JSObject? AtPoint(double x, double y);

    [JSImport("globalThis.Element.prototype.closest.call")]
    private static partial JSObject? Closest(JSObject element, string selector);

    [JSImport("globalThis.Element.prototype.getBoundingClientRect.call")]
    private static partial JSObject Rect(JSObject element);
}
