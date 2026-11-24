using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop;

/// <summary>Reads the platform from <c>navigator.platform</c>, a browser built-in, so the page names the command key as the user knows it.</summary>
[SupportedOSPlatform("browser")]
public static class BrowserPlatform
{
    /// <summary>Whether the page runs on an Apple system, where the command key is Cmd and not Ctrl.</summary>
    public static bool IsApple()
    {
        using JSObject navigator = JSHost.GlobalThis.GetPropertyAsJSObject("navigator")!;
        string platform = navigator.GetPropertyAsString("platform") ?? string.Empty;
        return platform.StartsWith("Mac", StringComparison.Ordinal) || platform.StartsWith("iP", StringComparison.Ordinal);
    }
}
