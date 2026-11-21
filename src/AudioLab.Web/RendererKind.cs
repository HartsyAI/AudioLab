namespace AudioLab.Web;

/// <summary>The lane-drawing candidates under test.</summary>
public enum RendererKind
{
    /// <summary>DOM and inline SVG, every clip in the page.</summary>
    Svg,

    /// <summary>DOM and inline SVG, only the clips in view.</summary>
    SvgVirtual,

    /// <summary>SkiaSharp on a WebGL canvas.</summary>
    SkiaGl,

    /// <summary>SkiaSharp rasterised on the CPU and copied to a canvas.</summary>
    SkiaCpu,

    /// <summary>The browser's 2D canvas, driven through built-ins with no script file.</summary>
    Canvas2D,
}
