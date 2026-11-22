using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Listens on the document through <c>addEventListener</c>, a browser built-in, once, and routes to the drag in progress.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class PointerTracker : IPointerTracker
{
    private Action<double, double>? _move;

    private Action<double, double>? _end;

    private Action? _cancel;

    private bool _installed;

    /// <inheritdoc />
    public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(cancel);
        Install();
        _move = move;
        _end = end;
        _cancel = cancel;
    }

    /// <inheritdoc />
    public void Stop()
    {
        _move = null;
        _end = null;
        _cancel = null;
    }

    private void Install()
    {
        if (_installed)
        {
            return;
        }
        _installed = true;
        JSObject document = Document();
        AddListener(document, "pointermove", press => _move?.Invoke(press.GetPropertyAsDouble("clientX"), press.GetPropertyAsDouble("clientY")));
        AddListener(document, "pointerup", press => Finish(_end, press));
        AddListener(document, "pointercancel", _ => Finish(_cancel));
        AddListener(document, "keydown", press =>
        {
            if (press.GetPropertyAsString("key") == "Escape")
            {
                Finish(_cancel);
            }
        });
    }

    private void Finish(Action<double, double>? end, JSObject press)
    {
        Action<double, double>? call = end;
        Stop();
        call?.Invoke(press.GetPropertyAsDouble("clientX"), press.GetPropertyAsDouble("clientY"));
    }

    private void Finish(Action? cancel)
    {
        Action? call = cancel;
        Stop();
        call?.Invoke();
    }

    [JSImport("globalThis.document.querySelector")]
    private static partial JSObject Query(string selector);

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddListener(JSObject target, string type, [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> callback);

    private static JSObject Document() => Query("html");
}
