using AudioLab.Web.Interop;
using AudioLab.Web.Rendering;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace AudioLab.Web.Components;

/// <summary>The lanes drawn by SkiaSharp onto a WebGL canvas (or its CPU raster fallback) through the package's own views.</summary>
public sealed partial class SkiaLaneSurface : LaneSurfaceBase
{
    private const double Overscan = 320;

    private readonly PaintWindow _window = new(Overscan);

    private BrowserElement? _element;

    private double _placedX = double.NaN;

    private double _placedY = double.NaN;

    private SKGLView? _glView;

    private SKCanvasView? _cpuView;

    private SkiaBackend? _backend;

    private LaneScene? _scene;

    private double _paintStart;

    /// <summary>Whether to draw on a WebGL canvas; false rasterises on the CPU.</summary>
    [Parameter]
    public bool Gpu { get; set; } = true;

    [Inject]
    private TokenSet Tokens { get; set; } = null!;

    [Inject]
    private HttpClient Http { get; set; } = null!;

    [Inject]
    private SkiaDiagnostics Diagnostics { get; set; } = null!;

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();
        _backend?.Dispose();
        _element?.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        List<string> failures = [];
        Dictionary<int, SKTypeface> faces = await SkiaFontLoader.LoadAsync(Http, "woff", failures);
        Diagnostics.FontFailures.AddRange(failures);
        _window.Recenter(View);
        _backend = new SkiaBackend(faces);
        _scene = new LaneScene(Session, View, Drag, Tokens);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        _element ??= Browser.Find("lane-skia");
        if (_backend is not null)
        {
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnViewChanged()
    {
        if (_backend is null)
        {
            return;
        }
        if (!_window.SizedFor(View))
        {
            _window.Recenter(View);
            StateHasChanged();
        }
        else if (!_window.Covers(View))
        {
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void Draw()
    {
        _paintStart = Browser.Now();
        _glView?.Invalidate();
        _cpuView?.Invalidate();
    }

    private void OnPaintGl(SKPaintGLSurfaceEventArgs e)
    {
        Paint(e.Surface.Canvas, e.Info.Width, e.Info.Height);
    }

    private void OnPaintCpu(SKPaintSurfaceEventArgs e)
    {
        Paint(e.Surface.Canvas, e.Info.Width, e.Info.Height);
    }

    private void Paint(SKCanvas canvas, int pixelWidth, int pixelHeight)
    {
        if (_scene is null || _backend is null)
        {
            return;
        }
        double start = Browser.Now();
        Diagnostics.LastSurface = $"{pixelWidth}x{pixelHeight} px for a {_window.Width}x{_window.Height} css canvas";
        bool viewOnly = Drag.Clip is not null && _window.Covers(View);
        if (!_window.Covers(View))
        {
            _window.Recenter(View);
        }
        float scale = (float)(pixelWidth / _window.Width);
        canvas.Save();
        canvas.Scale(scale);
        _backend.Attach(canvas);
        _scene.Paint(_backend, _window, viewOnly);
        canvas.Restore();
        if (_element is not null && (_placedX != _window.OriginX || _placedY != _window.OriginY))
        {
            _element.SetTranslation(_window.OriginX, _window.OriginY);
            _placedX = _window.OriginX;
            _placedY = _window.OriginY;
        }
        ReportDraw(start, 0, _scene.ClipsPainted);
    }
}
