using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Listens for key presses on the document through <c>addEventListener</c>, a browser built-in, once.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class ShortcutListener : IShortcutListener
{
    private static readonly string[] _editable = ["INPUT", "TEXTAREA", "SELECT"];

    private Func<KeyChord, bool>? _handler;

    private bool _installed;

    /// <inheritdoc />
    public void Listen(Func<KeyChord, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        if (_installed)
        {
            return;
        }
        _installed = true;
        AddListener(Query("html"), "keydown", press => Route(press));
    }

    /// <inheritdoc />
    public void Stop() => _handler = null;

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

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddListener(JSObject target, string type, [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> callback);
}
