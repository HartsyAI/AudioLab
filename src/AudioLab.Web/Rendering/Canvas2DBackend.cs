using AudioLab.Web.Interop;
using AudioLab.Web.Session;

namespace AudioLab.Web.Rendering;

/// <summary>Paints through the browser's own 2D canvas, driven from C# with no script file.</summary>
public sealed class Canvas2DBackend(Canvas2DContext context, string fontFamily, double pixelRatio) : IPaintBackend
{
    private readonly Dictionary<Rgba, string> _css = [];

    private readonly Dictionary<int, Path2DHandle> _paths = [];

    private readonly Dictionary<(int Weight, int Size), string> _fonts = [];

    private double _scale = pixelRatio;

    /// <inheritdoc />
    public int CallCount => context.Calls;

    /// <summary>Resizes the drawing buffer for a new size or pixel ratio.</summary>
    public void Resize(double width, double height, double ratio)
    {
        _scale = ratio;
        context.Resize((int)Math.Round(width * ratio), (int)Math.Round(height * ratio));
    }

    /// <inheritdoc />
    public void Begin(double x, double y, double width, double height, Rgba background)
    {
        context.Calls = 0;
        context.SetTransform(_scale, 0, 0, _scale, 0, 0);
        context.Save();
        context.BeginPath();
        context.RoundRect(x, y, width, height, 0);
        context.Clip();
        context.FillStyle(Css(background));
        context.FillRect(x, y, width, height);
    }

    /// <inheritdoc />
    public void FillRect(double x, double y, double width, double height, Rgba colour)
    {
        context.FillStyle(Css(colour));
        context.FillRect(x, y, width, height);
    }

    /// <inheritdoc />
    public void FillRoundRect(double x, double y, double width, double height, double radius, Rgba colour)
    {
        context.FillStyle(Css(colour));
        context.BeginPath();
        context.RoundRect(x, y, width, height, radius);
        context.Fill();
    }

    /// <inheritdoc />
    public void StrokeRoundRect(double x, double y, double width, double height, double radius, double lineWidth, Rgba colour)
    {
        context.StrokeStyle(Css(colour));
        context.LineWidth(lineWidth);
        context.BeginPath();
        context.RoundRect(x, y, width, height, radius);
        context.Stroke();
    }

    /// <inheritdoc />
    public void PushRoundClip(double x, double y, double width, double height, double radius)
    {
        context.Save();
        context.BeginPath();
        context.RoundRect(x, y, width, height, radius);
        context.Clip();
    }

    /// <inheritdoc />
    public void PopClip() => context.Restore();

    /// <inheritdoc />
    public void FillWave(ClipModel clip, double x, double y, double width, double height, Rgba colour)
    {
        if (!_paths.TryGetValue(clip.Id, out Path2DHandle? path))
        {
            path = context.CreatePath(WaveformSynth.PathData(clip.Peaks));
            _paths[clip.Id] = path;
        }
        context.FillStyle(Css(colour));
        context.SetTransform(_scale * width / 1000, 0, 0, _scale * height / 100, _scale * x, _scale * y);
        context.FillPath(path);
        context.SetTransform(_scale, 0, 0, _scale, 0, 0);
    }

    /// <inheritdoc />
    public void DrawText(string text, double x, double baseline, double size, int weight, Rgba colour)
    {
        context.Font(Font(weight, size));
        context.FillStyle(Css(colour));
        context.FillText(text, x, baseline);
    }

    /// <inheritdoc />
    public double MeasureText(string text, double size, int weight)
    {
        context.Font(Font(weight, size));
        return context.MeasureText(text);
    }

    /// <inheritdoc />
    public void End() => context.Restore();

    private string Css(Rgba colour)
    {
        if (!_css.TryGetValue(colour, out string? css))
        {
            css = colour.ToCss();
            _css[colour] = css;
        }
        return css;
    }

    private string Font(int weight, double size)
    {
        (int, int) key = (weight, (int)(size * 100));
        if (!_fonts.TryGetValue(key, out string? font))
        {
            font = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{weight} {size}px {fontFamily}");
            _fonts[key] = font;
        }
        return font;
    }
}
