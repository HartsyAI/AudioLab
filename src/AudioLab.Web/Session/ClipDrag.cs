using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>A clip being dragged: it follows the pointer, and where it lands comes from the server's rule.</summary>
/// <remarks>
/// Nothing is sent while it moves. A press that never travels <see cref="Threshold"/> pixels is a click, and a release where the clip began
/// sends nothing, so a drag that comes back leaves undo and redo as they were (D18).
/// </remarks>
public sealed class ClipDrag
{
    /// <summary>How far the pointer travels, in pixels, before a press becomes a drag.</summary>
    public const double Threshold = 3;

    private const double LaneInset = 4;

    private readonly TimelineGeometry _geometry;

    private readonly TrackLayout _layout;

    private readonly GridSnap _grid;

    private readonly double _grabX;

    private readonly double _grabY;

    private readonly double _startX;

    private readonly double _startY;

    private readonly TrackKind[] _kinds;

    private readonly double _originTop;

    private readonly bool _midi;

    /// <summary>Starts a drag of <paramref name="clip"/>, pressed at a point in lane pixels.</summary>
    /// <exception cref="ArgumentException">The clip is not in the document.</exception>
    public ClipDrag(ProjectDocument document, ClipId clip, TimelineGeometry geometry, TrackLayout layout, double laneX, double laneY)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        int row = document.Tracks.ToList().FindIndex(track => track.Clips.Any(c => c.Id == clip) || track.MidiClips.Any(c => c.Id == clip));
        if (row < 0)
        {
            throw new ArgumentException("That clip is not in the document.", nameof(clip));
        }
        Track track = document.Tracks[row];
        AudioClip? audio = track.Clips.FirstOrDefault(c => c.Id == clip);
        _midi = audio is null;
        MidiClip? midi = track.MidiClips.FirstOrDefault(c => c.Id == clip);
        Clip = clip;
        _geometry = geometry;
        _layout = layout;
        _grid = new GridSnap(document);
        _kinds = [.. document.Tracks.Select(candidate => candidate.Kind)];
        _tracks = [.. document.Tracks.Select(candidate => candidate.Id)];
        OriginRow = row;
        OriginTrack = track.Id;
        OriginStart = audio?.Start ?? midi!.Start;
        Width = Math.Max(2, geometry.Width(audio is null ? midi!.Length : ClipRules.VisibleLength(audio)) - 2);
        Height = layout.ClipHeights[row] - (2 * LaneInset) - 1;
        _originTop = layout.Tops[row] + LaneInset;
        _startX = laneX;
        _startY = laneY;
        _grabX = laneX - (geometry.X(OriginStart) + 1);
        _grabY = laneY - _originTop;
        TargetStart = OriginStart;
        TargetRow = row;
        Left = geometry.X(OriginStart) + 1;
        Top = _originTop;
    }

    private readonly TrackId[] _tracks;

    /// <summary>The clip being dragged.</summary>
    public ClipId Clip { get; }

    /// <summary>The track the clip began on.</summary>
    public TrackId OriginTrack { get; }

    /// <summary>The row of that track.</summary>
    public int OriginRow { get; }

    /// <summary>Where the clip began, in seconds.</summary>
    public double OriginStart { get; }

    /// <summary>The width of the clip in pixels.</summary>
    public double Width { get; }

    /// <summary>The height of the clip in pixels.</summary>
    public double Height { get; }

    /// <summary>Whether the pointer has travelled far enough for this to be a drag and not a click.</summary>
    public bool Active { get; private set; }

    /// <summary>The left edge of the lifted clip, which follows the pointer.</summary>
    public double Left { get; private set; }

    /// <summary>The top edge of the lifted clip, which follows the pointer.</summary>
    public double Top { get; private set; }

    /// <summary>Where the clip would start if it were released now, on the grid.</summary>
    public double TargetStart { get; private set; }

    /// <summary>The row it would land on.</summary>
    public int TargetRow { get; private set; }

    /// <summary>The left edge of the place it would land.</summary>
    public double LandingLeft => _geometry.X(TargetStart) + 1;

    /// <summary>The top edge of the place it would land.</summary>
    public double LandingTop => _layout.Tops[TargetRow] + LaneInset;

    /// <summary>Moves the pointer to a point in lane pixels.</summary>
    public void Move(double laneX, double laneY)
    {
        if (!Active && Math.Abs(laneX - _startX) < Threshold && Math.Abs(laneY - _startY) < Threshold)
        {
            return;
        }
        Active = true;
        Left = Math.Max(0, laneX - _grabX);
        Top = Math.Clamp(laneY - _grabY, 0, Math.Max(0, _layout.Total - Height));
        TargetStart = _grid.Snap(Math.Max(0, _geometry.Seconds(Left - 1)));
        int hovered = RowAt(laneY);
        if (KindFits(_kinds[hovered]))
        {
            TargetRow = hovered;
        }
    }

    /// <summary>Whether the drag was released and its move is on its way: the lifted clip then sits where it will land.</summary>
    public bool Landing { get; private set; }

    /// <summary>Puts the lifted clip on the place it lands, and keeps it there until the move is answered.</summary>
    public void Settle()
    {
        Landing = true;
        Left = LandingLeft;
        Top = LandingTop;
    }

    /// <summary>Releases the clip: the move to send, or null when it is a click or ended where it began.</summary>
    public ClipMove? Commit()
    {
        if (!Active)
        {
            return null;
        }
        bool sameTrack = TargetRow == OriginRow;
        if (sameTrack && Math.Abs(TargetStart - OriginStart) < TimelineRules.NoMove)
        {
            return null;
        }
        return new ClipMove(Clip, TargetStart, sameTrack ? null : _tracks[TargetRow]);
    }

    // An audio clip goes on an audio track and a MIDI clip on an instrument track (D22).
    private bool KindFits(TrackKind kind) => kind == (_midi ? TrackKind.Instrument : TrackKind.Audio);

    private int RowAt(double laneY)
    {
        for (int row = 0; row < _layout.Tops.Count; row++)
        {
            if (laneY < _layout.Tops[row] + _layout.Heights[row])
            {
                return row;
            }
        }
        return _layout.Tops.Count - 1;
    }
}
