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

    /// <inheritdoc />
    public void FocusIn(string containerId, string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        using JSObject? container = ById(containerId);
        if (container is null)
        {
            return;
        }
        using JSObject? element = Find(container, selector);
        if (element is not null)
        {
            FocusOn(element);
        }
    }

    [JSImport("globalThis.Element.prototype.querySelector.call")]
    private static partial JSObject? Find(JSObject container, string selector);

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? ById(string id);

    [JSImport("globalThis.HTMLElement.prototype.focus.call")]
    private static partial void FocusOn(JSObject element);
}
