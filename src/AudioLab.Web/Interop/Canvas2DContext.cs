using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>A 2D canvas context driven from C#. Every call crosses the interop boundary, so each is counted.</summary>
public sealed class Canvas2DContext : IDisposable
{
    private readonly JSObject _canvas;

    private readonly JSObject _context;

    private string _fillStyle = string.Empty;

    private string _strokeStyle = string.Empty;

    private string _font = string.Empty;

    private double _lineWidth = -1;

    private readonly Stack<(string Fill, string Stroke, string Font, double Line)> _saved = new();

    /// <summary>Creates a context over the given canvas element.</summary>
    public Canvas2DContext(BrowserElement canvas)
    {
        _canvas = canvas.Handle;
        _context = CanvasImports.GetContext(_canvas, "2d");
        Calls = 1;
    }

    /// <summary>Interop calls made since the counter was last reset.</summary>
    public int Calls { get; set; }

    /// <summary>Sets the size of the drawing buffer in device pixels.</summary>
    public void Resize(int width, int height)
    {
        _canvas.SetProperty("width", width);
        _canvas.SetProperty("height", height);
        _fillStyle = string.Empty;
        _strokeStyle = string.Empty;
        _font = string.Empty;
        _lineWidth = -1;
        Calls += 2;
    }

    /// <summary>Sets the fill colour, skipping the call when it already is that colour.</summary>
    public void FillStyle(string css)
    {
        if (_fillStyle != css)
        {
            _fillStyle = css;
            _context.SetProperty("fillStyle", css);
            Calls++;
        }
    }

    /// <summary>Sets the stroke colour, skipping the call when it already is that colour.</summary>
    public void StrokeStyle(string css)
    {
        if (_strokeStyle != css)
        {
            _strokeStyle = css;
            _context.SetProperty("strokeStyle", css);
            Calls++;
        }
    }

    /// <summary>Sets the stroke width, skipping the call when it already is that width.</summary>
    public void LineWidth(double width)
    {
        if (_lineWidth != width)
        {
            _lineWidth = width;
            _context.SetProperty("lineWidth", width);
            Calls++;
        }
    }

    /// <summary>Sets the font, skipping the call when it already is that font.</summary>
    public void Font(string css)
    {
        if (_font != css)
        {
            _font = css;
            _context.SetProperty("font", css);
            Calls++;
        }
    }

    /// <summary>Sets where text is anchored vertically.</summary>
    public void TextBaseline(string baseline)
    {
        _context.SetProperty("textBaseline", baseline);
        Calls++;
    }

    /// <summary>Sets the transform matrix.</summary>
    public void SetTransform(double a, double b, double c, double d, double e, double f)
    {
        CanvasImports.SetTransform(_context, a, b, c, d, e, f);
        Calls++;
    }

    /// <summary>Fills a rectangle with the current fill colour.</summary>
    public void FillRect(double x, double y, double width, double height)
    {
        CanvasImports.FillRect(_context, x, y, width, height);
        Calls++;
    }

    /// <summary>Starts a new path.</summary>
    public void BeginPath()
    {
        CanvasImports.BeginPath(_context);
        Calls++;
    }

    /// <summary>Adds a rounded rectangle to the current path.</summary>
    public void RoundRect(double x, double y, double width, double height, double radius)
    {
        CanvasImports.RoundRect(_context, x, y, width, height, radius);
        Calls++;
    }

    /// <summary>Fills the current path.</summary>
    public void Fill()
    {
        CanvasImports.Fill(_context);
        Calls++;
    }

    /// <summary>Strokes the current path.</summary>
    public void Stroke()
    {
        CanvasImports.Stroke(_context);
        Calls++;
    }

    /// <summary>Restricts drawing to the current path.</summary>
    public void Clip()
    {
        CanvasImports.Clip(_context);
        Calls++;
    }

    /// <summary>Saves the drawing state.</summary>
    public void Save()
    {
        _saved.Push((_fillStyle, _strokeStyle, _font, _lineWidth));
        CanvasImports.Save(_context);
        Calls++;
    }

    /// <summary>Restores the drawing state.</summary>
    public void Restore()
    {
        (_fillStyle, _strokeStyle, _font, _lineWidth) = _saved.Pop();
        CanvasImports.Restore(_context);
        Calls++;
    }

    /// <summary>Fills a retained path object.</summary>
    public void FillPath(Path2DHandle path)
    {
        CanvasImports.FillPath(_context, path.Handle);
        Calls++;
    }

    /// <summary>Draws text with its left edge at x and its baseline set by <see cref="TextBaseline"/>.</summary>
    public void FillText(string text, double x, double y)
    {
        CanvasImports.FillText(_context, text, x, y);
        Calls++;
    }

    /// <summary>Width of the text in the current font, in CSS pixels.</summary>
    public double MeasureText(string text)
    {
        using JSObject metrics = CanvasImports.MeasureText(_context, text);
        Calls++;
        return metrics.GetPropertyAsDouble("width");
    }

    /// <summary>Builds a retained path object from SVG path data, so a waveform is parsed once.</summary>
    public Path2DHandle CreatePath(string svgPathData)
    {
        using JSObject constructor = JSHost.GlobalThis.GetPropertyAsJSObject("Path2D")!;
        return new Path2DHandle(BrowserImports.Construct(constructor, [svgPathData]));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _context.Dispose();
        _canvas.Dispose();
    }
}
