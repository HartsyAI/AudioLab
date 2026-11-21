namespace AudioLab.Web.Interop;

/// <summary>The browser features the UI uses, all reached through built-ins; nothing here needs a script file.</summary>
public interface IBrowser
{
    /// <summary>Device pixels per CSS pixel.</summary>
    public double DevicePixelRatio { get; }

    /// <summary>Font size of the root element in pixels, the unit of every rem token.</summary>
    public double RootFontSize { get; }

    /// <summary>The browser's user agent string.</summary>
    public string UserAgent { get; }

    /// <summary>Inner width of the window in CSS pixels.</summary>
    public double WindowWidth { get; }

    /// <summary>Inner height of the window in CSS pixels.</summary>
    public double WindowHeight { get; }

    /// <summary>The query string of the page's address, including the leading question mark.</summary>
    public string LocationSearch { get; }

    /// <summary>Milliseconds on the page's monotonic clock.</summary>
    public double Now();

    /// <summary>Calls back once before the next paint with the frame's timestamp.</summary>
    public void RequestFrame(Action<double> callback);

    /// <summary>The value a custom property has on the root element, as written.</summary>
    public string RootToken(string name);

    /// <summary>The element with this id, or null.</summary>
    public BrowserElement? Find(string id);

    /// <summary>Calls back whenever the window is resized.</summary>
    public void OnWindowResize(Action callback);

    /// <summary>Loads a font face by its CSS descriptor, for example "600 10.5px Inter", before it is drawn.</summary>
    public Task LoadFontAsync(string descriptor);

    /// <summary>Creates a canvas drawing context over a canvas element.</summary>
    public Canvas2DContext CreateCanvasContext(BrowserElement canvas);
}
