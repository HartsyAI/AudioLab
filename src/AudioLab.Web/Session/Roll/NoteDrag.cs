using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Roll;

/// <summary>A drag on the notes: it follows the pointer on the grid the roll draws, shows where notes would land, and says what to send.</summary>
/// <remarks>
/// Nothing is sent while it moves. A move and a resize carry each note's absolute target (D23), clamped as a whole so the shape of the notes
/// is kept at an edge; a press that never travels <see cref="Threshold"/> pixels is a click and sends nothing. On release the targets are made
/// again from the clip as the page holds it then, so a note another client removed or moved meanwhile is not sent from a stale place.
/// </remarks>
public sealed class NoteDrag : IRollGesture
{
    /// <summary>How far the pointer travels, in pixels, before a press becomes a drag.</summary>
    public const double Threshold = 3;

    private readonly MidiClip _clip;

    private readonly RollGeometry _geometry;

    private readonly NoteIndex _index;

    private readonly GridSnap _grid;

    private readonly MidiNote[] _notes;

    private readonly MidiNote _grabbed;

    private readonly double _startX;

    private readonly double _startY;

    private readonly double _startTime;

    private readonly int _startPitch;

    private readonly int _velocity;

    private readonly HashSet<NoteId> _erased = [];

    private readonly List<MidiNote> _erasedNotes = [];

    private MidiNote[] _ghosts = [];

    private (double X, double Y, double Width, double Height)? _marquee;

    private (double X, double Y)? _last;

    private double _wantedDelta;

    private int _wantedPitch;

    private double _delta;

    private int _pitchDelta;

    private NoteDrag(
        NoteDragKind kind,
        ProjectDocument document,
        MidiClip clip,
        RollGeometry geometry,
        NoteIndex index,
        MidiNote[] notes,
        MidiNote grabbed,
        double x,
        double y,
        int velocity)
    {
        Kind = kind;
        _clip = clip;
        _geometry = geometry;
        _index = index;
        _grid = new GridSnap(document);
        _notes = notes;
        _grabbed = grabbed;
        _startX = x;
        _startY = y;
        _startTime = geometry.Seconds(x);
        _startPitch = geometry.PitchAt(y);
        _velocity = velocity;
    }

    /// <summary>What the drag does.</summary>
    public NoteDragKind Kind { get; }

    /// <inheritdoc />
    public bool Active { get; private set; }

    /// <summary>How many of the notes the drag acted on were gone from the clip when it was released.</summary>
    public int Lost { get; private set; }

    /// <summary>The notes as they would be if released now: the moved, resized or drawn ones.</summary>
    public IReadOnlyList<MidiNote> Ghosts
    {
        get
        {
            return Kind switch
            {
                NoteDragKind.Move => [.. _notes.Select(note => note with { Start = note.Start + _delta, Pitch = note.Pitch + _pitchDelta })],
                NoteDragKind.Resize => [.. _notes.Select(note => note with { Duration = note.Duration + _delta })],
                _ => _ghosts,
            };
        }
    }

    /// <summary>How far a move has carried the notes in time, in seconds; 0 for the other drags.</summary>
    public double Offset => Kind == NoteDragKind.Move ? _delta : 0;

    /// <summary>How far a move has carried the notes in pitch, in semitones; 0 for the other drags.</summary>
    public int PitchOffset => Kind == NoteDragKind.Move ? _pitchDelta : 0;

    /// <summary>The notes the drag acts on, as they were when it began.</summary>
    public IReadOnlyList<MidiNote> Origins => _notes;

    /// <summary>The notes an erase has crossed.</summary>
    public IReadOnlySet<NoteId> Erased => _erased;

    /// <summary>The notes an erase has crossed, in the order it crossed them.</summary>
    public IReadOnlyList<MidiNote> ErasedNotes => _erasedNotes;

    /// <summary>The rectangle of a marquee in pixels of the drawing, or null.</summary>
    public (double X, double Y, double Width, double Height)? Marquee => _marquee;

    /// <summary>A drag of the selected notes by one of them.</summary>
    public static NoteDrag ForMove(
        ProjectDocument document,
        MidiClip clip,
        RollGeometry geometry,
        NoteIndex index,
        IEnumerable<MidiNote> notes,
        MidiNote grabbed,
        double x,
        double y)
    {
        return new NoteDrag(NoteDragKind.Move, document, clip, geometry, index, [.. notes], grabbed, x, y, 0);
    }

    /// <summary>A drag of the right edge of the selected notes by the edge of one of them.</summary>
    public static NoteDrag ForResize(
        ProjectDocument document,
        MidiClip clip,
        RollGeometry geometry,
        NoteIndex index,
        IEnumerable<MidiNote> notes,
        MidiNote grabbed,
        double x,
        double y)
    {
        return new NoteDrag(NoteDragKind.Resize, document, clip, geometry, index, [.. notes], grabbed, x, y, 0);
    }

    /// <summary>A note drawn from the grid cell the press is in.</summary>
    public static NoteDrag ForDraw(ProjectDocument document, MidiClip clip, RollGeometry geometry, NoteIndex index, int velocity, double x, double y)
    {
        NoteDrag drag = new(NoteDragKind.Draw, document, clip, geometry, index, [], default, x, y, velocity);
        drag.Active = true;
        drag.Refresh(x);
        return drag;
    }

    /// <summary>An erase that begins at the press.</summary>
    public static NoteDrag ForErase(ProjectDocument document, MidiClip clip, RollGeometry geometry, NoteIndex index, double x, double y)
    {
        NoteDrag drag = new(NoteDragKind.Erase, document, clip, geometry, index, [], default, x, y, 0);
        drag.Active = true;
        drag.Cross(x, y);
        return drag;
    }

    /// <summary>A rectangle from the press.</summary>
    public static NoteDrag ForMarquee(ProjectDocument document, MidiClip clip, RollGeometry geometry, NoteIndex index, double x, double y)
    {
        return new NoteDrag(NoteDragKind.Marquee, document, clip, geometry, index, [], default, x, y, 0);
    }

    /// <summary>The notes the marquee holds now.</summary>
    public IReadOnlyList<NoteId> Picked()
    {
        if (_marquee is not { } box)
        {
            return [];
        }
        return
        [
            .. _index.InRectangle(
                _geometry.Seconds(box.X),
                _geometry.Seconds(box.X + box.Width),
                _geometry.PitchAt(box.Y + box.Height),
                _geometry.PitchAt(box.Y)),
        ];
    }

    /// <inheritdoc />
    public void Update(double x, double y)
    {
        if (!Active && Math.Abs(x - _startX) < Threshold && Math.Abs(y - _startY) < Threshold)
        {
            return;
        }
        Active = true;
        switch (Kind)
        {
            case NoteDragKind.Move:
                Shift(x, y);
                break;
            case NoteDragKind.Resize:
                Stretch(x);
                break;
            case NoteDragKind.Draw:
                Refresh(x);
                break;
            case NoteDragKind.Erase:
                Cross(x, y);
                break;
            default:
                _marquee = (Math.Min(x, _startX), Math.Min(y, _startY), Math.Abs(x - _startX), Math.Abs(y - _startY));
                break;
        }
    }

    /// <inheritdoc />
    public RollEdit? Commit(MidiClip current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!Active)
        {
            return null;
        }
        ClipId clip = _clip.Id;
        switch (Kind)
        {
            case NoteDragKind.Move:
                MidiNote[] moved = Rebase(current);
                if (moved.Length == 0)
                {
                    return null;
                }
                (double shift, int pitch) = FitMove(moved, _wantedDelta, _wantedPitch);
                if (Math.Abs(shift) < TimelineRules.NoMove && pitch == 0)
                {
                    return null;
                }
                NoteTarget[] targets = [.. moved.Select(note => new NoteTarget(note.Id, note.Start + shift, note.Pitch + pitch))];
                return RollEdit.Chunked(targets, (project, part) => new MoveNotes { Project = project, Clip = clip, Notes = part });
            case NoteDragKind.Resize:
                MidiNote[] resized = Rebase(current);
                double change = resized.Length == 0 ? 0 : FitStretch(resized, _wantedDelta);
                if (Math.Abs(change) < TimelineRules.NoMove)
                {
                    return null;
                }
                NoteLength[] lengths = [.. resized.Select(note => new NoteLength(note.Id, note.Duration + change))];
                return RollEdit.Chunked(lengths, (project, part) => new ResizeNotes { Project = project, Clip = clip, Notes = part });
            case NoteDragKind.Draw when _ghosts.Length == 1:
                MidiNote drawn = _ghosts[0];
                NewNote added = new(drawn.Pitch, drawn.Start, drawn.Duration, drawn.Velocity);
                return RollEdit.Of(project => new AddNotes { Project = project, Clip = clip, Notes = [added] }, selectCreated: true);
            case NoteDragKind.Erase when _erased.Count > 0:
                NoteId[] gone = [.. KeepPresent(_erasedNotes, current).Select(note => note.Id)];
                Lost = _erased.Count - gone.Length;
                return gone.Length == 0
                    ? null
                    : RollEdit.Chunked(gone, (project, part) => new DeleteNotes { Project = project, Clip = clip, Notes = part });
            default:
                return null;
        }
    }

    // The notes the drag acts on, as the clip holds them now; those another client removed are left out and counted in Lost.
    private MidiNote[] Rebase(MidiClip current)
    {
        MidiNote[] kept = KeepPresent(_notes, current);
        Lost = _notes.Length - kept.Length;
        return kept;
    }

    private static MidiNote[] KeepPresent(IReadOnlyCollection<MidiNote> notes, MidiClip current)
    {
        HashSet<NoteId> wanted = [.. notes.Select(note => note.Id)];
        Dictionary<NoteId, MidiNote> now = [];
        foreach (MidiNote note in current.Notes)
        {
            if (wanted.Contains(note.Id))
            {
                now[note.Id] = note;
            }
        }
        return [.. notes.Where(note => now.ContainsKey(note.Id)).Select(note => now[note.Id])];
    }

    // A pointer that has not travelled across in time leaves the notes where they are, so a drag of an off-grid note in pitch only, or one that
    // comes back to where it began, does not shift it to the grid.
    private bool HasMovedInTime(double x) => Math.Abs(x - _startX) >= Threshold;

    // The grabbed note goes to the grid line nearest to where the pointer put it, and every selected note keeps its place relative to it.
    private void Shift(double x, double y)
    {
        double wanted = _grabbed.Start + (_geometry.Seconds(x) - _startTime);
        _wantedDelta = HasMovedInTime(x) ? SnapLocal(wanted) - _grabbed.Start : 0;
        _wantedPitch = _geometry.PitchAt(y) - _startPitch;
        (_delta, _pitchDelta) = FitMove(_notes, _wantedDelta, _wantedPitch);
    }

    // Clamped as a whole: the earliest note stops at the clip start, the latest at the last start a note may have, the notes keep their shape.
    private (double Delta, int Pitch) FitMove(MidiNote[] notes, double delta, int pitch)
    {
        double latest = NoteRules.LatestStart(_clip.Length);
        double lowest = notes.Min(note => note.Start);
        double highest = notes.Max(note => note.Start);
        int lowPitch = notes.Min(note => note.Pitch);
        int highPitch = notes.Max(note => note.Pitch);
        return (Math.Max(-lowest, Math.Min(delta, latest - highest)), Math.Clamp(pitch, -lowPitch, RollGeometry.Pitches - 1 - highPitch));
    }

    // The right edge of the grabbed note goes to the nearest line, and every selected note grows or shrinks by the same amount.
    private void Stretch(double x)
    {
        double end = _grabbed.Start + _grabbed.Duration;
        _wantedDelta = HasMovedInTime(x) ? SnapLocal(end + (_geometry.Seconds(x) - _startTime)) - end : 0;
        _delta = FitStretch(_notes, _wantedDelta);
    }

    // The server's rule for a length (NoteRules.ResizeTo): at least the least length and at most the longest a note may be. A note may be made
    // to ring past the end of the clip, and one that already does can be lengthened.
    internal static double FitStretch(MidiNote[] notes, double delta)
    {
        double shortest = notes.Min(note => note.Duration);
        double longest = notes.Max(note => note.Duration);
        return Math.Max(NoteRules.MinDuration - shortest, Math.Min(delta, ProjectLimits.MaxSeconds - longest));
    }

    // One note from the cell the press is in to the line nearest the pointer, at least one grid step long and inside the clip.
    private void Refresh(double x)
    {
        double latest = NoteRules.LatestStart(_clip.Length);
        double start = Math.Clamp(FloorLocal(_startTime), 0, latest);
        double step = _grid.Step(_clip.Start + start);
        double end = Math.Max(start + step, SnapLocal(_geometry.Seconds(x)));
        double duration = Math.Max(NoteRules.MinDuration, Math.Min(end, _clip.Length) - start);
        _ghosts = [new MidiNote(new NoteId(0), _startPitch, start, duration, _velocity)];
    }

    // An erase takes every note the line from the last pointer sample to this one passes through, so a fast stroke leaves no gap between
    // samples: for each row of pitch the line crosses, the notes of that row within the stretch of time the line spends in it.
    private void Cross(double x, double y)
    {
        (double X, double Y) from = _last ?? (x, y);
        _last = (x, y);
        int first = _geometry.PitchAt(from.Y);
        int second = _geometry.PitchAt(y);
        for (int pitch = Math.Min(first, second); pitch <= Math.Max(first, second); pitch++)
        {
            double top = _geometry.RowTop(pitch);
            (double near, double far) = Within(from, (x, y), top, top + _geometry.RowHeight);
            foreach (MidiNote note in _index.InSpan(pitch, _geometry.Seconds(near), _geometry.Seconds(far)))
            {
                if (_erased.Add(note.Id))
                {
                    _erasedNotes.Add(note);
                }
            }
        }
    }

    // The two x of the part of a segment between two heights.
    private static (double Near, double Far) Within((double X, double Y) a, (double X, double Y) b, double top, double bottom)
    {
        double dy = b.Y - a.Y;
        if (Math.Abs(dy) < 1e-9)
        {
            return (Math.Min(a.X, b.X), Math.Max(a.X, b.X));
        }
        double enter = Math.Clamp((top - a.Y) / dy, 0, 1);
        double leave = Math.Clamp((bottom - a.Y) / dy, 0, 1);
        double x1 = a.X + (Math.Min(enter, leave) * (b.X - a.X));
        double x2 = a.X + (Math.Max(enter, leave) * (b.X - a.X));
        return (Math.Min(x1, x2), Math.Max(x1, x2));
    }

    private double SnapLocal(double seconds) => _grid.Snap(_clip.Start + seconds) - _clip.Start;

    private double FloorLocal(double seconds) => _grid.Floor(_clip.Start + seconds) - _clip.Start;
}
