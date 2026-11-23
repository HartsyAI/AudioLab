using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Adds a key listener, in the bubble phase, through <c>addEventListener</c>, a browser built-in, so no script file is needed.</summary>
[SupportedOSPlatform("browser")]
public sealed partial class KeyGuard : IKeyGuard
{
    private static readonly string[] _keys = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End"];

    /// <inheritdoc />
    public void PreventNavigationKeys(string containerId, string targetRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRole);
        JSObject? container = ById(containerId);
        if (container is null)
        {
            return;
        }
        AddListener(container, "keydown", press =>
        {
            using JSObject key = press;
            using JSObject target = press.GetPropertyAsJSObject("target")!;
            if (target.GetPropertyAsString("role") == targetRole && _keys.Contains(press.GetPropertyAsString("key")))
            {
                PreventDefault(press);
            }
        });
    }

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? ById(string id);

    [JSImport("globalThis.EventTarget.prototype.addEventListener.call")]
    private static partial void AddListener(JSObject target, string type, [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> callback);

    [JSImport("globalThis.Event.prototype.preventDefault.call")]
    private static partial void PreventDefault(JSObject press);
}
