using AudioLab.Web.Interop;
using AudioLab.Web.Rendering;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The lanes on the browser's own 2D canvas, drawn from C# through built-ins with no script file.</summary>
public sealed partial class CanvasLaneSurface : LaneSurfaceBase
{
    private Canvas2DBackend? _backend;

    private Canvas2DContext? _context;

    private LaneScene? _scene;

    private double _sizedWidth;

    private double _sizedHeight;

    [Inject]
    private TokenSet Tokens { get; set; } = null!;

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();
        _context?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            BrowserElement canvas = Browser.Find("lane-canvas") ?? throw new InvalidOperationException("The lane canvas is not in the page.");
            _context = Browser.CreateCanvasContext(canvas);
            _backend = new Canvas2DBackend(_context, Browser.RootToken("--font-ui"), Browser.DevicePixelRatio);
            _scene = new LaneScene(Session, View, Drag, Tokens);
        }
        if (_backend is not null && (_sizedWidth != View.ViewWidth || _sizedHeight != View.ViewHeight))
        {
            _sizedWidth = View.ViewWidth;
            _sizedHeight = View.ViewHeight;
            _backend.Resize(_sizedWidth, _sizedHeight, Browser.DevicePixelRatio);
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnViewChanged()
    {
        if (View.ViewWidth != _sizedWidth || View.ViewHeight != _sizedHeight)
        {
            StateHasChanged();
        }
        else
        {
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void Draw()
    {
        if (_scene is null || _backend is null)
        {
            return;
        }
        double start = Browser.Now();
        _scene.Paint(_backend);
        ReportDraw(start, _backend.CallCount, _scene.ClipsPainted);
    }
}
