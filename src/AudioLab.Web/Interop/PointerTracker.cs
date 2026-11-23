using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Listens on the document through <c>addEventListener</c>, a browser built-in, for the length of one drag and no longer.</summary>
/// <remarks>
/// The listeners are added when a drag begins and removed when it ends, so a page that is not dragging runs no code for a pointer move. Every
/// event handed to the page is released as soon as it is read, and so are the functions that were added.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed partial class PointerTracker : IPointerTracker
{
    private readonly List<(string Type, JSObject Handler)> _installed = [];

    private Action<double, double>? _move;

    private Action<double, double>? _end;

    private Action? _cancel;

    private JSObject? _document;

    /// <inheritdoc />
    public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(cancel);
        Stop();
        _move = move;
        _end = end;
        _cancel = cancel;
        _document = Query("html");
        Add("pointermove", press =>
        {
            Action<double, double>? call = _move;
            call?.Invoke(press.GetPropertyAsDouble("clientX"), press.GetPropertyAsDouble("clientY"));
        });
        Add("pointerup", press => Finish(_end, press));
        Add("pointercancel", _ => Finish(_cancel));
        Add("keydown", press =>
        {
            if (press.GetPropertyAsString("key") == "Escape")
            {
                Finish(_cancel);
            }
        });
    }

    /// <inheritdoc />
    public void Stop()
    {
        _move = null;
        _end = null;
        _cancel = null;
        if (_document is not null)
        {
            foreach ((string type, JSObject handler) in _installed)
            {
                Remove(_document, type, handler);
                handler.Dispose();
            }
            _document.Dispose();
            _document = null;
        }
        _installed.Clear();
    }

    private void Add(string type, Action<JSObject> handler)
    {
        // The event is released when the handler returns; the function is kept so that it can be removed again.
        JSObject function = Function(press =>
        {
            using JSObject released = press;
            handler(press);
        });
        _installed.Add((type, function));
        AddListener(_document!, type, function);
    }

    private void Finish(Action<double, double>? end, JSObject press)
    {
        double x = press.GetPropertyAsDouble("clientX");
        double y = press.GetPropertyAsDouble("clientY");
        Stop();
        end?.Invoke(x, y);
    }

    private void Finish(Action? cancel)
    {
        Stop();
        cancel?.Invoke();
    }

    private static JSObject Function(Action<JSObject> callback)
    {
        using JSObject wrapped = Wrap(callback);
        return wrapped.GetPropertyAsJSObject("0")!;
    }

    [JSImport("globalThis.document.querySelector")]
    private static partial JSObject Query(string selector);

    // Array.of(f) is [f]: the one way to get the function that the marshaller made for a callback back as an object.
    [JSImport("globalThis.Array.of")]
    private static partial JSObject Wrap([JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> callback);

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddListener(JSObject target, string type, JSObject listener);

    [JSImport("globalThis.EventTarget.prototype.removeEventListener.call")]
    private static partial void Remove(JSObject target, string type, JSObject listener);
}
