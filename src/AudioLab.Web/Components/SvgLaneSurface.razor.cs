using System.Globalization;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The lanes as DOM and inline SVG, like the mock. Optionally only the clips near the view are in the page.</summary>
public sealed partial class SvgLaneSurface : LaneSurfaceBase
{
    private const int TrackMargin = 2;

    private int _windowFirstTrack;

    private int _windowLastTrack = int.MaxValue;

    private double _windowLeft = double.NegativeInfinity;

    private double _windowRight = double.PositiveInfinity;

    private bool _windowSet;

    /// <summary>Whether only the clips near the view are rendered.</summary>
    [Parameter]
    public bool Virtualize { get; set; }

    private int FirstTrack => Virtualize && _windowSet ? _windowFirstTrack : 0;

    private int LastTrack => Virtualize && _windowSet ? _windowLastTrack : Session.Tracks.Count - 1;

    private string BackgroundStyle
    {
        get
        {
            string strong = (View.BarWidth * 4).ToString("0.##", CultureInfo.InvariantCulture);
            string weak = View.BarWidth.ToString("0.##", CultureInfo.InvariantCulture);
            return $"background-image: linear-gradient(90deg, var(--grid-line-strong) 1px, transparent 1px), linear-gradient(90deg, var(--grid-line) 1px, transparent 1px); background-size: {strong}px 100%, {weak}px 100%";
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        Drag.StateChanged += OnStateChanged;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();
        Drag.StateChanged -= OnStateChanged;
    }

    /// <inheritdoc />
    protected override void Draw()
    {
        double start = Browser.Now();
        StateHasChanged();
        ReportDraw(start, 0, 0);
    }

    /// <inheritdoc />
    protected override void OnViewChanged()
    {
        if (!Virtualize)
        {
            return;
        }
        double left = View.ScrollLeft;
        double right = left + View.ViewWidth;
        bool inside = _windowSet && View.FirstTrack >= _windowFirstTrack && View.LastTrack <= _windowLastTrack && left >= _windowLeft && right <= _windowRight;
        if (!inside)
        {
            _windowSet = true;
            _windowFirstTrack = Math.Max(0, View.FirstTrack - TrackMargin);
            _windowLastTrack = Math.Min(Session.Tracks.Count - 1, View.LastTrack + TrackMargin);
            _windowLeft = left - View.ViewWidth;
            _windowRight = right + View.ViewWidth;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnDragChanged(ClipModel? clip)
    {
    }

    private bool InWindow(ClipModel clip)
    {
        if (!Virtualize || !_windowSet)
        {
            return true;
        }
        double left = View.ClipLeft(clip);
        return left + View.ClipWidth(clip) >= _windowLeft && left <= _windowRight;
    }

    private void OnStateChanged() => Invalidate();
}
