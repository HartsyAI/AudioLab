using AudioLab.Web.Interop;
using AudioLab.Web.Rendering;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The lanes on the browser's own 2D canvas, drawn from C# through built-ins with no script file. The canvas is wider than the view and scrolls with the page, so it is repainted only when the view nears its edge.</summary>
public sealed partial class CanvasLaneSurface : LaneSurfaceBase
{
    private const double Overscan = 320;

    private readonly PaintWindow _window = new(Overscan);

    private Canvas2DBackend? _backend;

    private Canvas2DContext? _context;

    private BrowserElement? _element;

    private LaneScene? _scene;

    private double _sizedWidth;

    private double _sizedHeight;

    private double _placedX = double.NaN;

    private double _placedY = double.NaN;

    [Inject]
    private TokenSet Tokens { get; set; } = null!;

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();
        _context?.Dispose();
        _element?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _window.Recenter(View);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _element = Browser.Find("lane-canvas") ?? throw new InvalidOperationException("The lane canvas is not in the page.");
            _context = Browser.CreateCanvasContext(_element);
            _backend = new Canvas2DBackend(_context, Browser.RootToken("--font-ui"), Browser.DevicePixelRatio);
            _scene = new LaneScene(Session, View, Drag, Tokens);
        }
        if (_backend is not null && (_sizedWidth != _window.Width || _sizedHeight != _window.Height))
        {
            _sizedWidth = _window.Width;
            _sizedHeight = _window.Height;
            _backend.Resize(_sizedWidth, _sizedHeight, Browser.DevicePixelRatio);
            Draw();
        }
    }

    /// <inheritdoc />
    protected override void OnViewChanged()
    {
        if (!_window.SizedFor(View))
        {
            _window.Recenter(View);
            StateHasChanged();
        }
        else if (!_window.Covers(View))
        {
            Draw();
        }
    }

    /// <inheritdoc />
    protected override void Draw()
    {
        if (_scene is null || _backend is null || _element is null)
        {
            return;
        }
        double start = Browser.Now();
        bool viewOnly = Drag.Clip is not null && _window.Covers(View);
        if (!_window.Covers(View))
        {
            _window.Recenter(View);
        }
        _scene.Paint(_backend, _window, viewOnly);
        if (_placedX != _window.OriginX || _placedY != _window.OriginY)
        {
            _element.SetTranslation(_window.OriginX, _window.OriginY);
            _placedX = _window.OriginX;
            _placedY = _window.OriginY;
        }
        ReportDraw(start, _backend.CallCount, _scene.ClipsPainted);
    }
}
