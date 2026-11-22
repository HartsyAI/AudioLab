using AudioLab.Web.Interop;
using AudioLab.Web.Rendering;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The arrangement: one scroll container holding the ruler, the track headers and the lane surface under test.</summary>
public sealed partial class Timeline : ComponentBase, IDisposable
{
    private BrowserElement? _scroller;

    private BrowserElement? _lanes;

    private bool _ready;

    private double _originLeft;

    private double _originTop;

    private double _downScrollLeft;

    private double _downScrollTop;

    private double _headerWidth;

    private double _rulerHeight;

    [Inject]
    private SessionModel Session { get; set; } = null!;

    [Inject]
    private TimelineView View { get; set; } = null!;

    [Inject]
    private DragController Drag { get; set; } = null!;

    [Inject]
    private IBrowser Browser { get; set; } = null!;

    [Inject]
    private TokenSet Tokens { get; set; } = null!;

    [Inject]
    private SpikeOptions Options { get; set; } = null!;

    private double HeaderWidth => _headerWidth;

    /// <inheritdoc />
    public void Dispose()
    {
        _scroller?.Dispose();
        _lanes?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _headerWidth = Tokens.Length("--header-column");
        _rulerHeight = (Tokens.Length("--ruler-row") * 2) + 1;
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }
        _scroller = Browser.Find("timeline");
        _lanes = Browser.Find("lanes");
        if (_scroller is null || _lanes is null)
        {
            throw new InvalidOperationException("The timeline elements are not in the page.");
        }
        _scroller.OnScroll(() => View.SetScroll(_scroller.ScrollLeft, _scroller.ScrollTop));
        Browser.OnWindowResize(Measure);
        Measure();
        _ready = true;
        StateHasChanged();
    }

    private void Measure()
    {
        if (_scroller is not null)
        {
            View.SetViewport(_scroller.ClientWidth - _headerWidth, _scroller.ClientHeight - _rulerHeight);
        }
    }

    private void OnPointerDown(PointerEventArgs e)
    {
        if (e.Button != 0 || _scroller is null || _lanes is null)
        {
            return;
        }
        _originLeft = _lanes.RectLeft();
        _originTop = _lanes.RectTop();
        _downScrollLeft = View.ScrollLeft;
        _downScrollTop = View.ScrollTop;
        double x = e.ClientX - _originLeft;
        double y = e.ClientY - _originTop;
        if (x < 0 || e.ClientY < _scroller.RectTop() + _rulerHeight)
        {
            return;
        }
        ClipModel? hit = View.HitTest(x, y);
        Session.SelectClip(hit);
        if (hit is null)
        {
            Drag.NotifySelectionChanged();
            return;
        }
        _scroller.CapturePointer((int)e.PointerId);
        Drag.Begin(hit, x, y);
    }

    private void OnPointerMove(PointerEventArgs e)
    {
        if (Drag.Clip is not null)
        {
            Drag.Move(e.ClientX - _originLeft + (View.ScrollLeft - _downScrollLeft), e.ClientY - _originTop + (View.ScrollTop - _downScrollTop));
        }
    }

    private void OnPointerUp(PointerEventArgs e) => Drag.Commit();

    private void OnPointerCancel(PointerEventArgs e) => Drag.Cancel();

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            Drag.Cancel();
        }
    }
}
