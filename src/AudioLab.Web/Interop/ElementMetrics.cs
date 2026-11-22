using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Reads <c>getBoundingClientRect</c>, a browser built-in.</summary>
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

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? ById(string id);

    [JSImport("globalThis.Element.prototype.getBoundingClientRect.call")]
    private static partial JSObject Rect(JSObject element);
}
