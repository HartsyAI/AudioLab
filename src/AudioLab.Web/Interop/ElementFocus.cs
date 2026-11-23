using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Focuses elements through <c>HTMLElement.focus</c>, a browser built-in.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class ElementFocus : IElementFocus
{
    /// <inheritdoc />
    public void Focus(string elementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        using JSObject? element = ById(elementId);
        if (element is not null)
        {
            FocusOn(element);
        }
    }

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? ById(string id);

    [JSImport("globalThis.HTMLElement.prototype.focus.call")]
    private static partial void FocusOn(JSObject element);
}
