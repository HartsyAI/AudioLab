using AudioLab.Web.Session;

namespace AudioLab.Web.Rendering;

/// <summary>The part of the lanes a canvas holds: the view plus a margin on every side, so scrolling inside it needs no repaint.</summary>
public sealed class PaintWindow(double overscan)
{
    private bool _valid;

    /// <summary>Left edge in lane coordinates.</summary>
    public double OriginX { get; private set; }

    /// <summary>Top edge in lane coordinates.</summary>
    public double OriginY { get; private set; }

    /// <summary>Width in CSS pixels.</summary>
    public double Width { get; private set; }

    /// <summary>Height in CSS pixels.</summary>
    public double Height { get; private set; }

    /// <summary>First track at least partly inside the window.</summary>
    public int FirstTrack(TimelineView view) => Math.Max(0, (int)(OriginY / view.LaneHeight));

    /// <summary>Last track at least partly inside the window.</summary>
    public int LastTrack(TimelineView view) => Math.Min(view.Session.Tracks.Count - 1, (int)((OriginY + Height) / view.LaneHeight));

    /// <summary>Whether the view sits far enough inside the window that scrolling needs no repaint yet.</summary>
    public bool Covers(TimelineView view)
    {
        double margin = overscan / 4;
        bool horizontal = (OriginX == 0 || view.ScrollLeft >= OriginX + margin)
            && (OriginX + Width >= view.ContentWidth || view.ScrollLeft + view.ViewWidth <= OriginX + Width - margin);
        bool vertical = (OriginY == 0 || view.ScrollTop >= OriginY + margin)
            && (OriginY + Height >= view.ContentHeight || view.ScrollTop + view.ViewHeight <= OriginY + Height - margin);
        return _valid && horizontal && vertical;
    }

    /// <summary>Whether the window has the size the view needs.</summary>
    public bool SizedFor(TimelineView view) => _valid && Width == Math.Min(view.ViewWidth + (2 * overscan), view.ContentWidth) && Height == Math.Min(view.ViewHeight + (2 * overscan), view.ContentHeight);

    /// <summary>Centres the window on the view.</summary>
    public void Recenter(TimelineView view)
    {
        Width = Math.Min(view.ViewWidth + (2 * overscan), view.ContentWidth);
        Height = Math.Min(view.ViewHeight + (2 * overscan), view.ContentHeight);
        OriginX = Math.Clamp(view.ScrollLeft - overscan, 0, view.ContentWidth - Width);
        OriginY = Math.Clamp(view.ScrollTop - overscan, 0, view.ContentHeight - Height);
        _valid = true;
    }
}
