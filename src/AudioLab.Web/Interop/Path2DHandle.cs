using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>A retained browser path object, built once from SVG path data.</summary>
public sealed class Path2DHandle(JSObject handle) : IDisposable
{
    /// <summary>The underlying object.</summary>
    internal JSObject Handle { get; } = handle;

    /// <inheritdoc />
    public void Dispose() => Handle.Dispose();
}
