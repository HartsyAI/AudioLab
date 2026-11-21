using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>A handle to one DOM element, with the few properties and calls the UI needs.</summary>
public sealed class BrowserElement(JSObject handle) : IDisposable
{
    /// <summary>The underlying object, for other interop types in this folder.</summary>
    internal JSObject Handle { get; } = handle;

    /// <summary>Horizontal scroll offset in CSS pixels.</summary>
    public double ScrollLeft
    {
        get => Handle.GetPropertyAsDouble("scrollLeft");
        set => Handle.SetProperty("scrollLeft", value);
    }

    /// <summary>Vertical scroll offset in CSS pixels.</summary>
    public double ScrollTop
    {
        get => Handle.GetPropertyAsDouble("scrollTop");
        set => Handle.SetProperty("scrollTop", value);
    }

    /// <summary>Width of the element's padding box in CSS pixels.</summary>
    public double ClientWidth => Handle.GetPropertyAsDouble("clientWidth");

    /// <summary>Height of the element's padding box in CSS pixels.</summary>
    public double ClientHeight => Handle.GetPropertyAsDouble("clientHeight");

    /// <summary>Left edge in viewport pixels.</summary>
    public double RectLeft()
    {
        using JSObject rect = BrowserImports.GetBoundingClientRect(Handle);
        return rect.GetPropertyAsDouble("left");
    }

    /// <summary>Top edge in viewport pixels.</summary>
    public double RectTop()
    {
        using JSObject rect = BrowserImports.GetBoundingClientRect(Handle);
        return rect.GetPropertyAsDouble("top");
    }

    /// <summary>Calls back on every scroll of this element, straight from the browser's event.</summary>
    public void OnScroll(Action callback) => BrowserImports.AddEventListener(Handle, "scroll", callback);

    /// <summary>Gives the element keyboard focus.</summary>
    public void Focus() => BrowserImports.Focus(Handle);

    /// <summary>Routes the pointer's events to this element until it is released.</summary>
    public void CapturePointer(int pointerId) => BrowserImports.SetPointerCapture(Handle, pointerId);

    /// <inheritdoc />
    public void Dispose() => Handle.Dispose();
}
