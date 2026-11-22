using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Reads tokens from the root element's computed style through browser built-ins, with no script file.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class TokenReader : ITokenReader
{
    /// <inheritdoc />
    public string Read(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        using JSObject root = Root();
        using JSObject style = ComputedStyle(root);
        return PropertyValue(style, token).Trim();
    }

    [JSImport("globalThis.document.querySelector")]
    private static partial JSObject Query(string selector);

    [JSImport("globalThis.getComputedStyle")]
    private static partial JSObject ComputedStyle(JSObject element);

    [JSImport("globalThis.CSSStyleDeclaration.prototype.getPropertyValue.call")]
    private static partial string PropertyValue(JSObject style, string name);

    private static JSObject Root() => Query(":root");
}
