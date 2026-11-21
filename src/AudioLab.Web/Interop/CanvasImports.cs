using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>Canvas 2D calls made through the prototype's own functions, so the context is passed as the receiver.</summary>
internal static partial class CanvasImports
{
    [JSImport("globalThis.HTMLCanvasElement.prototype.getContext.call")]
    internal static partial JSObject GetContext(JSObject canvas, string kind);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.fillRect.call")]
    internal static partial void FillRect(JSObject context, double x, double y, double width, double height);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.clearRect.call")]
    internal static partial void ClearRect(JSObject context, double x, double y, double width, double height);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.setTransform.call")]
    internal static partial void SetTransform(JSObject context, double a, double b, double c, double d, double e, double f);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.save.call")]
    internal static partial void Save(JSObject context);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.restore.call")]
    internal static partial void Restore(JSObject context);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.beginPath.call")]
    internal static partial void BeginPath(JSObject context);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.roundRect.call")]
    internal static partial void RoundRect(JSObject context, double x, double y, double width, double height, double radius);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.clip.call")]
    internal static partial void Clip(JSObject context);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.fill.call")]
    internal static partial void Fill(JSObject context);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.fill.call")]
    internal static partial void FillPath(JSObject context, JSObject path);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.stroke.call")]
    internal static partial void Stroke(JSObject context);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.fillText.call")]
    internal static partial void FillText(JSObject context, string text, double x, double y);

    [JSImport("globalThis.CanvasRenderingContext2D.prototype.measureText.call")]
    internal static partial JSObject MeasureText(JSObject context, string text);
}
