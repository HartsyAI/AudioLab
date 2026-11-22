using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>Bindings to browser built-ins. Every name resolves on globalThis, so no script file ships with them.</summary>
internal static partial class BrowserImports
{
    [JSImport("globalThis.performance.now")]
    internal static partial double Now();

    [JSImport("globalThis.requestAnimationFrame")]
    internal static partial int RequestAnimationFrame([JSMarshalAs<JSType.Function<JSType.Number>>] Action<double> callback);

    [JSImport("globalThis.document.getElementById")]
    internal static partial JSObject? GetElementById(string id);

    [JSImport("globalThis.getComputedStyle")]
    internal static partial JSObject GetComputedStyle(JSObject element);

    [JSImport("globalThis.CSSStyleDeclaration.prototype.getPropertyValue.call")]
    internal static partial string GetPropertyValue(JSObject style, string name);

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    internal static partial void AddEventListener(JSObject target, string type, [JSMarshalAs<JSType.Function>] Action callback);

    [JSImport("globalThis.Element.prototype.getBoundingClientRect.call")]
    internal static partial JSObject GetBoundingClientRect(JSObject element);

    [JSImport("globalThis.Element.prototype.setPointerCapture.call")]
    internal static partial void SetPointerCapture(JSObject element, int pointerId);

    [JSImport("globalThis.HTMLElement.prototype.focus.call")]
    internal static partial void Focus(JSObject element);

    [JSImport("globalThis.document.fonts.load")]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    internal static partial Task<JSObject> LoadFont(string descriptor);

    [JSImport("globalThis.Reflect.construct")]
    internal static partial JSObject Construct(JSObject constructor, [JSMarshalAs<JSType.Array<JSType.String>>] string[] arguments);
}
