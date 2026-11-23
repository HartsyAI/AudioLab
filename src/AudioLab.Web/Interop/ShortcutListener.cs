using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Listens for key presses on the document through <c>addEventListener</c>, a browser built-in, while a handler is set.</summary>
/// <remarks>The listener is added by <see cref="Listen"/> and removed by <see cref="Stop"/>; each event is released once it has been read.</remarks>
[SupportedOSPlatform("browser")]
public sealed partial class ShortcutListener : IShortcutListener
{
    private static readonly string[] _editable = ["INPUT", "TEXTAREA", "SELECT"];

    private Func<KeyChord, bool>? _handler;

    private JSObject? _document;

    private JSObject? _function;

    /// <inheritdoc />
    public void Listen(Func<KeyChord, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        if (_function is not null)
        {
            return;
        }
        _document = Query("html");
        using JSObject wrapped = Wrap(press =>
        {
            using JSObject released = press;
            Route(press);
        });
        _function = wrapped.GetPropertyAsJSObject("0")!;
        AddListener(_document, "keydown", _function);
    }

    /// <inheritdoc />
    public void Stop()
    {
        _handler = null;
        if (_function is null || _document is null)
        {
            return;
        }
        Remove(_document, "keydown", _function);
        _function.Dispose();
        _document.Dispose();
        _function = null;
        _document = null;
    }

    private bool Route(JSObject press)
    {
        if (_handler is null || press.GetPropertyAsBoolean("defaultPrevented") || InEditableOrDialog(press))
        {
            return false;
        }
        KeyChord chord = new(
            press.GetPropertyAsString("key") ?? string.Empty,
            press.GetPropertyAsBoolean("ctrlKey") || press.GetPropertyAsBoolean("metaKey"),
            press.GetPropertyAsBoolean("shiftKey"),
            press.GetPropertyAsBoolean("altKey"));
        bool handled = _handler(chord);
        if (handled)
        {
            PreventDefault(press);
        }
        return handled;
    }

    private static bool InEditableOrDialog(JSObject press)
    {
        using JSObject? target = press.GetPropertyAsJSObject("target");
        if (target is null)
        {
            return false;
        }
        string tag = target.GetPropertyAsString("tagName") ?? string.Empty;
        using JSObject? dialog = Closest(target, "dialog");
        return _editable.Contains(tag, StringComparer.OrdinalIgnoreCase) || target.GetPropertyAsBoolean("isContentEditable") || dialog is not null;
    }

    [JSImport("globalThis.document.querySelector")]
    private static partial JSObject Query(string selector);

    [JSImport("globalThis.Element.prototype.closest.call")]
    private static partial JSObject? Closest(JSObject element, string selector);

    [JSImport("globalThis.Event.prototype.preventDefault.call")]
    private static partial void PreventDefault(JSObject press);

    // Array.of(f) is [f]: the one way to get the function that the marshaller made for a callback back as an object.
    [JSImport("globalThis.Array.of")]
    private static partial JSObject Wrap([JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> callback);

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddListener(JSObject target, string type, JSObject listener);

    [JSImport("globalThis.EventTarget.prototype.removeEventListener.call")]
    private static partial void Remove(JSObject target, string type, JSObject listener);
}
