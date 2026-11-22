namespace AudioLab.Web.Session;

/// <summary>Where the arrangement is scrolled and how it is zoomed. Every lane surface reads this one object.</summary>
public sealed class TimelineView
{
    /// <summary>Creates a view for the given session and zoom.</summary>
    public TimelineView(SessionModel session, double barWidth, double laneHeight, double clipInset, double clipHeader)
    {
        Session = session;
        BarWidth = barWidth;
        LaneHeight = laneHeight;
        ClipInset = clipInset;
        ClipHeader = clipHeader;
    }

    /// <summary>Raised when scroll or size changed.</summary>
    public event Action? Changed;

    /// <summary>The session shown.</summary>
    public SessionModel Session { get; }

    /// <summary>Width of one bar in CSS pixels.</summary>
    public double BarWidth { get; }

    /// <summary>Height of one lane in CSS pixels.</summary>
    public double LaneHeight { get; }

    /// <summary>Gap between a lane's edge and its clips.</summary>
    public double ClipInset { get; }

    /// <summary>Height of a clip's title bar.</summary>
    public double ClipHeader { get; }

    /// <summary>Horizontal scroll offset.</summary>
    public double ScrollLeft { get; private set; }

    /// <summary>Vertical scroll offset.</summary>
    public double ScrollTop { get; private set; }

    /// <summary>Width of the visible lane area.</summary>
    public double ViewWidth { get; private set; }

    /// <summary>Height of the visible lane area.</summary>
    public double ViewHeight { get; private set; }

    /// <summary>Width of all the lanes.</summary>
    public double ContentWidth => Session.Bars * BarWidth;

    /// <summary>Height of all the lanes.</summary>
    public double ContentHeight => Session.Tracks.Count * LaneHeight;

    /// <summary>Height of a clip: the lane less its inset above and below and the one-pixel lane line.</summary>
    public double ClipHeight => LaneHeight - (2 * ClipInset) - 1;

    /// <summary>First track at least partly in view.</summary>
    public int FirstTrack => Math.Max(0, (int)(ScrollTop / LaneHeight));

    /// <summary>Last track at least partly in view.</summary>
    public int LastTrack => Math.Min(Session.Tracks.Count - 1, (int)((ScrollTop + ViewHeight) / LaneHeight));

    /// <summary>Left edge of a clip in lane coordinates.</summary>
    public double ClipLeft(ClipModel clip) => (clip.StartBar * BarWidth) + 1;

    /// <summary>Width of a clip.</summary>
    public double ClipWidth(ClipModel clip) => (clip.LengthBars * BarWidth) - 2;

    /// <summary>Records a scroll and tells listeners when it moved.</summary>
    public void SetScroll(double left, double top)
    {
        if (left != ScrollLeft || top != ScrollTop)
        {
            ScrollLeft = left;
            ScrollTop = top;
            Changed?.Invoke();
        }
    }

    /// <summary>Records the size of the visible lane area and tells listeners when it changed.</summary>
    public void SetViewport(double width, double height)
    {
        if (width != ViewWidth || height != ViewHeight)
        {
            ViewWidth = width;
            ViewHeight = height;
            Changed?.Invoke();
        }
    }

    /// <summary>The clip under a point in lane coordinates, or null.</summary>
    public ClipModel? HitTest(double x, double y)
    {
        int track = (int)(y / LaneHeight);
        if (track < 0 || track >= Session.Tracks.Count)
        {
            return null;
        }
        double inLane = y - (track * LaneHeight);
        if (inLane < ClipInset || inLane > ClipInset + ClipHeight)
        {
            return null;
        }
        foreach (ClipModel clip in Session.Tracks[track].Clips)
        {
            double left = ClipLeft(clip);
            if (x >= left && x <= left + ClipWidth(clip))
            {
                return clip;
            }
        }
        return null;
    }
}
