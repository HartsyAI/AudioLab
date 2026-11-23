using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Clicks elements through <c>HTMLElement.click</c>, a browser built-in.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class ElementClick : IElementClick
{
    /// <inheritdoc />
    public void Click(string elementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        using JSObject? element = ById(elementId);
        if (element is not null)
        {
            ClickOn(element);
        }
    }

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? ById(string id);

    [JSImport("globalThis.HTMLElement.prototype.click.call")]
    private static partial void ClickOn(JSObject element);
}
