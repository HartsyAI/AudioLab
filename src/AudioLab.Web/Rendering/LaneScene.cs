using AudioLab.Web.Session;

namespace AudioLab.Web.Rendering;

/// <summary>Paints the visible part of the lanes through a backend: grid, tracks, clips, loop, playhead and a drag in progress.</summary>
public sealed class LaneScene(SessionModel session, TimelineView view, DragController drag, TokenSet tokens)
{
    private const double WaveInset = 0;

    private const double RegionMargin = 48;

    private readonly string?[] _titles = new string?[session.Clips.Count];

    private double _titleBarWidth;

    private readonly Rgba _lane = tokens.Colour("--surface-lane");

    private readonly Rgba _laneLine = tokens.Colour("--lane-line");

    private readonly Rgba _grid = tokens.Colour("--grid-line");

    private readonly Rgba _gridStrong = tokens.Colour("--grid-line-strong");

    private readonly Rgba _playhead = tokens.Colour("--playhead");

    private readonly Rgba _ink = tokens.Colour("--ink");

    private readonly Rgba _accent = tokens.Colour("--accent");

    private readonly double _radius = tokens.Length("--radius-sm");

    private readonly double _radiusSmall = tokens.Length("--radius-xs");

    private readonly double _labelSize = tokens.Length("--font-size-label");

    /// <summary>Clips painted by the last call.</summary>
    public int ClipsPainted { get; private set; }

    /// <summary>Paints one frame: the whole window, or with viewOnly just the view and a margin, which is all a drag changes.</summary>
    public void Paint(IPaintBackend backend, PaintWindow window, bool viewOnly)
    {
        if (_titleBarWidth != view.BarWidth)
        {
            _titleBarWidth = view.BarWidth;
            Array.Clear(_titles);
        }
        double width = window.Width;
        double height = window.Height;
        double left = window.OriginX;
        double top = window.OriginY;
        double bar = view.BarWidth;
        double laneHeight = view.LaneHeight;
        ClipsPainted = 0;
        double regionLeft = viewOnly ? Math.Max(0, view.ScrollLeft - left - RegionMargin) : 0;
        double regionRight = viewOnly ? Math.Min(width, view.ScrollLeft - left + view.ViewWidth + RegionMargin) : width;
        double regionTop = viewOnly ? Math.Max(0, view.ScrollTop - top - RegionMargin) : 0;
        double regionBottom = viewOnly ? Math.Min(height, view.ScrollTop - top + view.ViewHeight + RegionMargin) : height;
        backend.Begin(regionLeft, regionTop, regionRight - regionLeft, regionBottom - regionTop, _lane);
        PaintGrid(backend, left + regionLeft, regionRight - regionLeft, left, regionTop, regionBottom - regionTop, bar);
        int last = Math.Min(window.LastTrack(view), (int)((top + regionBottom) / laneHeight));
        for (int t = Math.Max(window.FirstTrack(view), (int)((top + regionTop) / laneHeight)); t <= last; t++)
        {
            TrackModel track = session.Tracks[t];
            double y = (t * laneHeight) - top;
            if (track.Selected)
            {
                backend.FillRect(0, y, width, laneHeight, _playhead.Scale(0.03));
            }
            backend.FillRect(0, y + laneHeight - 1, width, 1, _laneLine);
            Rgba colour = tokens.Colour(track.ColourToken);
            List<ClipModel> clips = track.Clips;
            for (int i = 0; i < clips.Count; i++)
            {
                ClipModel clip = clips[i];
                double x = view.ClipLeft(clip) - left;
                double clipWidth = view.ClipWidth(clip);
                if (x + clipWidth >= regionLeft && x <= regionRight)
                {
                    PaintClip(backend, clip, colour, x, y + view.ClipInset, clipWidth, track.Muted ? 0.4 : 1, clip.Selected);
                }
            }
        }
        PaintOverlays(backend, left, width, height, bar);
        PaintDrag(backend, left, top);
        backend.End();
    }

    private void PaintGrid(IPaintBackend backend, double from, double span, double windowLeft, double y, double height, double bar)
    {
        int first = (int)(from / bar);
        int lastBar = (int)((from + span) / bar) + 1;
        for (int k = first; k <= lastBar; k++)
        {
            double x = (k * bar) - windowLeft;
            backend.FillRect(x, y, 1, height, _grid);
            if (k % 4 == 0)
            {
                backend.FillRect(x, y, 1, height, _gridStrong);
            }
        }
    }

    private void PaintOverlays(IPaintBackend backend, double left, double width, double height, double bar)
    {
        double loopX = (session.LoopStartBar * bar) - left;
        double loopWidth = session.LoopBars * bar;
        if (loopX + loopWidth >= 0 && loopX <= width)
        {
            backend.FillRect(loopX, 0, loopWidth, height, _accent.Scale(0.09));
        }
        double headX = Math.Round(session.PlayheadBar * bar) - left;
        if (headX >= 0 && headX <= width)
        {
            backend.FillRect(headX, 0, 1, height, _playhead);
        }
    }

    private void PaintDrag(IPaintBackend backend, double left, double top)
    {
        ClipModel? clip = drag.Clip;
        if (clip is null)
        {
            return;
        }
        double bar = view.BarWidth;
        double clipWidth = view.ClipWidth(clip);
        double landX = (drag.TargetBar * bar) + 1 - left;
        double landY = (drag.TargetTrack * view.LaneHeight) + view.ClipInset - top;
        backend.StrokeRoundRect(landX + 0.75, landY + 0.75, clipWidth - 1.5, view.ClipHeight - 1.5, _radius, 1.5, _accent);
        double x = drag.X - left;
        double y = drag.Y - top;
        for (int i = 4; i >= 1; i--)
        {
            backend.FillRoundRect(x - (i * 2), y - (i * 2) + 6, clipWidth + (i * 4), view.ClipHeight + (i * 4), _radius + (i * 2), new Rgba(0, 0, 0, 0.12));
        }
        Rgba colour = tokens.Colour(session.Tracks[clip.Track].ColourToken);
        PaintClip(backend, clip, colour, x, y, clipWidth, 1, true);
    }

    private void PaintClip(IPaintBackend backend, ClipModel clip, Rgba colour, double x, double y, double width, double opacity, bool selected)
    {
        double height = view.ClipHeight;
        double header = view.ClipHeader;
        ClipsPainted++;
        backend.FillRoundRect(x, y, width, height, _radius, colour.Scale((selected ? 0.30 : 0.17) * opacity));
        if (selected)
        {
            backend.StrokeRoundRect(x + 0.75, y + 0.75, width - 1.5, height - 1.5, _radius, 1.5, _playhead.Scale(opacity));
        }
        else
        {
            backend.StrokeRoundRect(x + 0.5, y + 0.5, width - 1, height - 1, _radius, 1, colour.Scale(0.5 * opacity));
        }
        backend.PushRoundClip(x, y, width, height, _radius);
        backend.FillRect(x, y, width, header, colour.Scale(opacity));
        backend.DrawText(Title(backend, clip, width), x + 6, y + 11.8, _labelSize, 600, _ink.Scale(opacity));
        double bodyY = y + header + WaveInset;
        double bodyHeight = height - header - WaveInset;
        if (clip.IsMidi)
        {
            Rgba noteColour = colour.Scale(opacity);
            foreach (NoteSpan note in clip.Notes)
            {
                backend.FillRoundRect(x + (note.X / 100 * width), bodyY + (note.Y / 100 * bodyHeight), note.Width / 100 * width, 3, _radiusSmall, noteColour);
            }
        }
        else
        {
            backend.FillWave(clip, x, bodyY, width, bodyHeight, colour.Scale(opacity));
        }
        backend.PopClip();
    }

    private string Title(IPaintBackend backend, ClipModel clip, double width)
    {
        string? title = _titles[clip.Id];
        if (title is null)
        {
            double available = width - 12;
            double natural = backend.MeasureText(clip.Name, _labelSize, 600);
            if (natural <= available)
            {
                title = clip.Name;
            }
            else
            {
                int keep = Math.Max(0, (int)(clip.Name.Length * (available / natural)) - 1);
                title = string.Concat(clip.Name.AsSpan(0, Math.Min(keep, clip.Name.Length)), "…");
            }
            _titles[clip.Id] = title;
        }
        return title;
    }
}
