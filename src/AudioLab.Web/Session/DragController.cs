namespace AudioLab.Web.Session;

/// <summary>A clip drag: previewed locally on every pointer move and committed as one change on release.</summary>
public sealed class DragController(TimelineView view)
{
    private const double SnapPerBar = 16;

    private double _grabX;

    private double _grabY;

    private double _originX;

    private double _originY;

    /// <summary>Raised on every change a surface must redraw for, including each pointer move.</summary>
    public event Action<ClipModel?>? Changed;

    /// <summary>Raised when a drag begins or ends or the selection changed, not on each pointer move.</summary>
    public event Action? StateChanged;

    /// <summary>The clip being dragged, or null.</summary>
    public ClipModel? Clip { get; private set; }

    /// <summary>Left edge of the lifted clip, following the pointer.</summary>
    public double X { get; private set; }

    /// <summary>Top edge of the lifted clip, following the pointer.</summary>
    public double Y { get; private set; }

    /// <summary>Snapped start, in bars, where the clip would land.</summary>
    public double TargetBar { get; private set; }

    /// <summary>Track where the clip would land.</summary>
    public int TargetTrack { get; private set; }

    /// <summary>Starts dragging a clip grabbed at a point in lane coordinates.</summary>
    public void Begin(ClipModel clip, double laneX, double laneY)
    {
        Clip = clip;
        _originX = view.ClipLeft(clip);
        _originY = (clip.Track * view.LaneHeight) + view.ClipInset;
        _grabX = laneX - _originX;
        _grabY = laneY - _originY;
        X = _originX;
        Y = _originY;
        TargetBar = clip.StartBar;
        TargetTrack = clip.Track;
        clip.Revision++;
        Changed?.Invoke(clip);
        StateChanged?.Invoke();
    }

    /// <summary>Tells surfaces the selection changed without a drag.</summary>
    public void NotifySelectionChanged()
    {
        Changed?.Invoke(null);
        StateChanged?.Invoke();
    }

    /// <summary>Moves the lifted clip to a point in lane coordinates and recomputes where it would land.</summary>
    public void Move(double laneX, double laneY)
    {
        if (Clip is null)
        {
            return;
        }
        double maxX = view.ContentWidth - view.ClipWidth(Clip) - 2;
        X = Math.Clamp(laneX - _grabX, 0, maxX);
        Y = Math.Clamp(laneY - _grabY, 0, view.ContentHeight - view.LaneHeight);
        double bars = Math.Round((X - 1) / view.BarWidth * SnapPerBar) / SnapPerBar;
        TargetBar = Math.Clamp(bars, 0, view.Session.Bars - Clip.LengthBars);
        TargetTrack = Math.Clamp((int)((Y + (view.LaneHeight / 2)) / view.LaneHeight), 0, view.Session.Tracks.Count - 1);
        Changed?.Invoke(Clip);
    }

    /// <summary>Drops the clip at its snapped position, the one command a gesture produces.</summary>
    public void Commit()
    {
        ClipModel? clip = Clip;
        if (clip is null)
        {
            return;
        }
        if (clip.Track != TargetTrack)
        {
            view.Session.Tracks[clip.Track].Clips.Remove(clip);
            view.Session.Tracks[TargetTrack].Clips.Add(clip);
            clip.Track = TargetTrack;
        }
        clip.StartBar = TargetBar;
        End(clip);
    }

    /// <summary>Puts the clip back where it was.</summary>
    public void Cancel()
    {
        if (Clip is not null)
        {
            End(Clip);
        }
    }

    private void End(ClipModel clip)
    {
        Clip = null;
        clip.Revision++;
        Changed?.Invoke(clip);
        StateChanged?.Invoke();
    }
}
