using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>The browser, reached through built-ins only.</summary>
public sealed class BrowserHost : IBrowser
{
    private readonly JSObject _root = JSHost.GlobalThis.GetPropertyAsJSObject("document")!.GetPropertyAsJSObject("documentElement")!;

    private readonly JSObject _style;

    /// <summary>Creates the host and reads the root element's computed style.</summary>
    public BrowserHost() => _style = BrowserImports.GetComputedStyle(_root);

    /// <inheritdoc />
    public double DevicePixelRatio => JSHost.GlobalThis.GetPropertyAsDouble("devicePixelRatio");

    /// <inheritdoc />
    public double RootFontSize => double.Parse(BrowserImports.GetPropertyValue(_style, "font-size").Replace("px", string.Empty, StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public string UserAgent => JSHost.GlobalThis.GetPropertyAsJSObject("navigator")!.GetPropertyAsString("userAgent") ?? string.Empty;

    /// <inheritdoc />
    public double WindowWidth => JSHost.GlobalThis.GetPropertyAsDouble("innerWidth");

    /// <inheritdoc />
    public double WindowHeight => JSHost.GlobalThis.GetPropertyAsDouble("innerHeight");

    /// <inheritdoc />
    public string LocationSearch => JSHost.GlobalThis.GetPropertyAsJSObject("location")!.GetPropertyAsString("search") ?? string.Empty;

    /// <inheritdoc />
    public string VisibilityState => JSHost.GlobalThis.GetPropertyAsJSObject("document")!.GetPropertyAsString("visibilityState") ?? string.Empty;

    /// <inheritdoc />
    public bool HasFocus => AudioImports.DocumentHasFocus();

    /// <inheritdoc />
    public double Now() => BrowserImports.Now();

    /// <inheritdoc />
    public void RequestFrame(Action<double> callback) => BrowserImports.RequestAnimationFrame(callback);

    /// <inheritdoc />
    public string RootToken(string name) => BrowserImports.GetPropertyValue(_style, name).Trim();

    /// <inheritdoc />
    public BrowserElement? Find(string id)
    {
        JSObject? handle = BrowserImports.GetElementById(id);
        return handle is null ? null : new BrowserElement(handle);
    }

    /// <inheritdoc />
    public void OnWindowResize(Action callback) => BrowserImports.AddEventListener(JSHost.GlobalThis, "resize", callback);

    /// <inheritdoc />
    public async Task LoadFontAsync(string descriptor)
    {
        using JSObject faces = await BrowserImports.LoadFont(descriptor);
    }
}
