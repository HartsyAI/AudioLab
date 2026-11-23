using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Roll;

/// <summary>A drag on the notes: it follows the pointer on the grid the roll draws, shows where notes would land, and says what to send.</summary>
/// <remarks>
/// Nothing is sent while it moves. A move and a resize carry each note's absolute target (D23), clamped as a whole so the shape of the notes
/// is kept at an edge; a press that never travels <see cref="Threshold"/> pixels is a click and sends nothing.
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

    private MidiNote[] _ghosts = [];

    private (double X, double Y, double Width, double Height)? _marquee;

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

    /// <summary>The notes as they would be if released now: the moved, resized or drawn ones.</summary>
    public IReadOnlyList<MidiNote> Ghosts => _ghosts;

    /// <summary>How far a move has carried the notes in time, in seconds; 0 for the other drags.</summary>
    public double Offset => Kind == NoteDragKind.Move ? _delta : 0;

    /// <summary>How far a move has carried the notes in pitch, in semitones; 0 for the other drags.</summary>
    public int PitchOffset => Kind == NoteDragKind.Move ? _pitchDelta : 0;

    /// <summary>The notes the drag acts on, as they were when it began.</summary>
    public IReadOnlyList<MidiNote> Origins => _notes;

    /// <summary>The notes an erase has crossed.</summary>
    public IReadOnlySet<NoteId> Erased => _erased;

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
    public RollEdit? Commit()
    {
        if (!Active)
        {
            return null;
        }
        ClipId clip = _clip.Id;
        switch (Kind)
        {
            case NoteDragKind.Move when Math.Abs(_delta) >= TimelineRules.NoMove || _pitchDelta != 0:
                ValueList<NoteTarget> targets = [.. _notes.Select(note => new NoteTarget(note.Id, note.Start + _delta, note.Pitch + _pitchDelta))];
                return RollEdit.Of(project => new MoveNotes { Project = project, Clip = clip, Notes = targets });
            case NoteDragKind.Resize when Math.Abs(_delta) >= TimelineRules.NoMove:
                ValueList<NoteLength> lengths = [.. _notes.Select(note => new NoteLength(note.Id, note.Duration + _delta))];
                return RollEdit.Of(project => new ResizeNotes { Project = project, Clip = clip, Notes = lengths });
            case NoteDragKind.Draw when _ghosts.Length == 1:
                MidiNote drawn = _ghosts[0];
                NewNote added = new(drawn.Pitch, drawn.Start, drawn.Duration, drawn.Velocity);
                return RollEdit.Of(project => new AddNotes { Project = project, Clip = clip, Notes = [added] }, selectCreated: true);
            case NoteDragKind.Erase when _erased.Count > 0:
                ValueList<NoteId> gone = [.. _erased];
                return RollEdit.Of(project => new DeleteNotes { Project = project, Clip = clip, Notes = gone });
            default:
                return null;
        }
    }

    // The grabbed note goes to the grid line nearest to where the pointer put it, and every selected note keeps its place relative to it.
    private void Shift(double x, double y)
    {
        double wanted = _grabbed.Start + (_geometry.Seconds(x) - _startTime);
        double delta = SnapLocal(wanted) - _grabbed.Start;
        double latest = NoteRules.LatestStart(_clip.Length);
        double lowest = _notes.Min(note => note.Start);
        double highest = _notes.Max(note => note.Start);
        _delta = Math.Max(-lowest, Math.Min(delta, latest - highest));
        int pitch = _geometry.PitchAt(y) - _startPitch;
        _pitchDelta = Math.Clamp(pitch, -_notes.Min(note => note.Pitch), RollGeometry.Pitches - 1 - _notes.Max(note => note.Pitch));
        _ghosts = [.. _notes.Select(note => note with { Start = note.Start + _delta, Pitch = note.Pitch + _pitchDelta })];
    }

    // The right edge of the grabbed note goes to the nearest line, and every selected note grows or shrinks by the same amount.
    private void Stretch(double x)
    {
        double end = _grabbed.Start + _grabbed.Duration;
        double delta = SnapLocal(end + (_geometry.Seconds(x) - _startTime)) - end;
        double shortest = _notes.Min(note => note.Duration);
        double room = Math.Max(0, _clip.Length - _notes.Max(note => note.Start + note.Duration));
        _delta = Math.Max(NoteRules.MinDuration - shortest, Math.Min(delta, room));
        _ghosts = [.. _notes.Select(note => note with { Duration = note.Duration + _delta })];
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

    private void Cross(double x, double y)
    {
        MidiNote? hit = _index.HitTest(_geometry.Seconds(x), _geometry.PitchAt(y), 0, out _);
        if (hit is { } note)
        {
            _erased.Add(note.Id);
        }
    }

    private double SnapLocal(double seconds) => _grid.Snap(_clip.Start + seconds) - _clip.Start;

    private double FloorLocal(double seconds) => _grid.Floor(_clip.Start + seconds) - _clip.Start;
}
