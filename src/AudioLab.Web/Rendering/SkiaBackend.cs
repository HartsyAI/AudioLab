using AudioLab.Web.Session;
using SkiaSharp;

namespace AudioLab.Web.Rendering;

/// <summary>Paints through SkiaSharp onto the canvas a view hands it for each frame.</summary>
public sealed class SkiaBackend(IReadOnlyDictionary<int, SKTypeface> faces) : IPaintBackend, IDisposable
{
    private readonly Dictionary<int, SKPath> _paths = [];

    private readonly Dictionary<(int Weight, int Size), SKFont> _fonts = [];

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    private readonly SKRoundRect _clip = new();

    private SKCanvas? _canvas;

    /// <inheritdoc />
    public int CallCount => 0;

    /// <summary>Gives the backend the canvas for the frame about to be painted.</summary>
    public void Attach(SKCanvas canvas) => _canvas = canvas;

    /// <inheritdoc />
    public void Begin(double width, double height, Rgba background) => Canvas.Clear(Colour(background));

    /// <inheritdoc />
    public void FillRect(double x, double y, double width, double height, Rgba colour)
    {
        _fill.Color = Colour(colour);
        Canvas.DrawRect((float)x, (float)y, (float)width, (float)height, _fill);
    }

    /// <inheritdoc />
    public void FillRoundRect(double x, double y, double width, double height, double radius, Rgba colour)
    {
        _fill.Color = Colour(colour);
        Canvas.DrawRoundRect((float)x, (float)y, (float)width, (float)height, (float)radius, (float)radius, _fill);
    }

    /// <inheritdoc />
    public void StrokeRoundRect(double x, double y, double width, double height, double radius, double lineWidth, Rgba colour)
    {
        _stroke.Color = Colour(colour);
        _stroke.StrokeWidth = (float)lineWidth;
        Canvas.DrawRoundRect((float)x, (float)y, (float)width, (float)height, (float)radius, (float)radius, _stroke);
    }

    /// <inheritdoc />
    public void PushRoundClip(double x, double y, double width, double height, double radius)
    {
        Canvas.Save();
        _clip.SetRect(new SKRect((float)x, (float)y, (float)(x + width), (float)(y + height)), (float)radius, (float)radius);
        Canvas.ClipRoundRect(_clip, SKClipOperation.Intersect, true);
    }

    /// <inheritdoc />
    public void PopClip() => Canvas.Restore();

    /// <inheritdoc />
    public void FillWave(ClipModel clip, double x, double y, double width, double height, Rgba colour)
    {
        if (!_paths.TryGetValue(clip.Id, out SKPath? path))
        {
            path = BuildPath(clip.Peaks);
            _paths[clip.Id] = path;
        }
        _fill.Color = Colour(colour);
        Canvas.Save();
        Canvas.Translate((float)x, (float)y);
        Canvas.Scale((float)(width / 1000), (float)(height / 100));
        Canvas.DrawPath(path, _fill);
        Canvas.Restore();
    }

    /// <inheritdoc />
    public void DrawText(string text, double x, double baseline, double size, int weight, Rgba colour)
    {
        _fill.Color = Colour(colour);
        Canvas.DrawText(text, (float)x, (float)baseline, SKTextAlign.Left, Font(weight, size), _fill);
    }

    /// <inheritdoc />
    public double MeasureText(string text, double size, int weight) => Font(weight, size).MeasureText(text);

    /// <inheritdoc />
    public void End()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (SKPath path in _paths.Values)
        {
            path.Dispose();
        }
        foreach (SKFont font in _fonts.Values)
        {
            font.Dispose();
        }
        _fill.Dispose();
        _stroke.Dispose();
        _clip.Dispose();
    }

    private SKCanvas Canvas => _canvas ?? throw new InvalidOperationException("No canvas is attached; call Attach before painting.");

    private static SKColor Colour(Rgba colour) => new(colour.R, colour.G, colour.B, (byte)Math.Round(Math.Clamp(colour.A, 0, 1) * 255));

    private static SKPath BuildPath(float[] peaks)
    {
        using SKPathBuilder builder = new();
        int count = peaks.Length;
        builder.MoveTo(0, 50);
        for (int i = 0; i < count; i++)
        {
            builder.LineTo(i / (float)(count - 1) * 1000, 50 - (peaks[i] * 44));
        }
        for (int i = count - 1; i >= 0; i--)
        {
            builder.LineTo(i / (float)(count - 1) * 1000, 50 + (peaks[i] * 44));
        }
        builder.Close();
        return builder.Detach();
    }

    private SKFont Font(int weight, double size)
    {
        (int, int) key = (weight, (int)(size * 100));
        if (!_fonts.TryGetValue(key, out SKFont? font))
        {
            SKTypeface face = faces.TryGetValue(weight, out SKTypeface? found) ? found : SKTypeface.Default;
            font = new SKFont(face, (float)size) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
            _fonts[key] = font;
        }
        return font;
    }
}
