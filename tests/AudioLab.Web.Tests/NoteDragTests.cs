using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>The gestures on the notes: where they would land and what they send, which the server's rules must agree with.</summary>
public sealed class NoteDragTests
{
    // 80 pixels a second, 12 a row; 120 beats a minute, the grid auto (a sixteenth: 0.125 s, 10 pixels).
    private static readonly ProjectId _project = ProjectId.From("song");

    private static MidiNote Note(int id, int pitch, double start, double duration) => new(new NoteId(id), pitch, start, duration, 90);

    private readonly MidiClip _clip = Song.Midi("m", 0, 4, Note(1, 60, 1, 0.5), Note(2, 64, 1.5, 0.5), Note(3, 67, 3, 0.5));

    private ProjectDocument Document => Song.Empty().With(Song.WithMidi("keys", _clip));

    private RollGeometry Geometry => new(80, 12, _clip.Length);

    private static double X(double seconds) => seconds * 80;

    private static double Y(int pitch) => ((127 - pitch) * 12) + 6;

    private NoteDrag Move(IEnumerable<MidiNote> notes, MidiNote grabbed)
    {
        return NoteDrag.ForMove(Document, _clip, Geometry, NoteIndex.Of(_clip), notes, grabbed, X(grabbed.Start) + 2, Y(grabbed.Pitch));
    }

    private static T Only<T>(RollEdit? edit)
        where T : Command
    {
        return Assert.IsType<T>(Assert.Single(Assert.IsType<RollEdit>(edit).Build(_project)));
    }

    /// <summary>A move gives every selected note an absolute target, the grabbed note on the grid and the others at the same offset.</summary>
    [Fact]
    public void AMoveGivesEverySelectedNoteAnAbsoluteTargetWithTheGrabbedOneOnTheGrid()
    {
        MidiNote[] selected = [_clip.Notes[0], _clip.Notes[1]];
        NoteDrag drag = Move(selected, selected[0]);

        drag.Update(X(1) + 2 + 33, Y(60) - 24);

        MoveNotes sent = Only<MoveNotes>(drag.Commit());
        Assert.Equal(_clip.Id, sent.Clip);
        Assert.Equal(1.375, sent.Notes[0].Start, 9);
        Assert.Equal(62, sent.Notes[0].Pitch);
        Assert.Equal(1.875, sent.Notes[1].Start, 9);
        Assert.Equal(66, sent.Notes[1].Pitch);
        Assert.Equal(2, drag.Ghosts.Count);
    }

    /// <summary>A move stopped at an edge keeps the shape of the notes, and one that comes back sends nothing.</summary>
    [Fact]
    public void AMoveStoppedAtAnEdgeKeepsTheShapeAndOneThatComesBackSendsNothing()
    {
        MidiNote[] selected = [_clip.Notes[0], _clip.Notes[2]];
        NoteDrag drag = Move(selected, selected[0]);

        drag.Update(X(1) + 2 + 400, Y(60));

        MoveNotes sent = Only<MoveNotes>(drag.Commit());
        Assert.Equal(3.98, sent.Notes[1].Start, 9);
        Assert.Equal(1.98, sent.Notes[0].Start, 9);
        drag.Update(X(1) + 2, Y(60));
        Assert.Null(drag.Commit());
    }

    /// <summary>A press that never travels three pixels is a click and sends nothing.</summary>
    [Fact]
    public void APressThatNeverTravelsIsAClick()
    {
        NoteDrag drag = Move([_clip.Notes[0]], _clip.Notes[0]);

        drag.Update(X(1) + 3, Y(60) + 1);

        Assert.False(drag.Active);
        Assert.Null(drag.Commit());
    }

    /// <summary>A resize puts the grabbed edge on the grid and every selected note grows by the same amount, none below the least length.</summary>
    [Fact]
    public void AResizeMovesTheEdgeToTheGridAndEveryNoteGrowsByTheSameAmount()
    {
        MidiNote[] selected = [_clip.Notes[0], _clip.Notes[1]];
        NoteDrag drag = NoteDrag.ForResize(Document, _clip, Geometry, NoteIndex.Of(_clip), selected, selected[0], X(1.5) - 2, Y(60));

        drag.Update(X(1.5) - 2 + 21, Y(60));

        ResizeNotes sent = Only<ResizeNotes>(drag.Commit());
        Assert.Equal(0.75, sent.Notes[0].Duration, 9);
        Assert.Equal(0.75, sent.Notes[1].Duration, 9);
        drag.Update(X(1.5) - 2 - 500, Y(60));
        Assert.Equal(0.02, Only<ResizeNotes>(drag.Commit()).Notes[0].Duration, 9);
    }

    /// <summary>A drawn note starts in the cell that was pressed, is at least one step long and goes in with the velocity asked for.</summary>
    [Fact]
    public void ADrawnNoteStartsInThePressedCellAndIsAtLeastOneStepLong()
    {
        NoteDrag drag = NoteDrag.ForDraw(Document, _clip, Geometry, NoteIndex.Of(_clip), 77, X(2.06), Y(70));

        AddNotes tap = Only<AddNotes>(drag.Commit());
        Assert.Equal(new NewNote(70, 2, 0.125, 77), tap.Notes[0]);
        drag.Update(X(2.06) + 40, Y(70));
        AddNotes dragged = Only<AddNotes>(drag.Commit());
        Assert.Equal(0.5, dragged.Notes[0].Duration ?? 0, 9);
        NoteDrag late = NoteDrag.ForDraw(Document, _clip, Geometry, NoteIndex.Of(_clip), 100, X(3.99), Y(60));
        Assert.Equal(3.875, Only<AddNotes>(late.Commit()).Notes[0].Start, 9);
    }

    /// <summary>An erase removes the notes the pointer crossed.</summary>
    [Fact]
    public void AnEraseRemovesTheNotesThePointerCrossed()
    {
        NoteDrag drag = NoteDrag.ForErase(Document, _clip, Geometry, NoteIndex.Of(_clip), X(1.1), Y(60));

        drag.Update(X(1.7), Y(64));
        drag.Update(X(1.8), Y(80));

        DeleteNotes sent = Only<DeleteNotes>(drag.Commit());
        Assert.Equal([new NoteId(1), new NoteId(2)], sent.Notes.OrderBy(note => note.Value));
        Assert.Equal(2, drag.Erased.Count);
    }

    /// <summary>A marquee picks the notes inside its rectangle and sends nothing.</summary>
    [Fact]
    public void AMarqueePicksTheNotesInsideItsRectangleAndSendsNothing()
    {
        NoteDrag drag = NoteDrag.ForMarquee(Document, _clip, Geometry, NoteIndex.Of(_clip), X(0.5), Y(70));

        drag.Update(X(2.2), Y(58));

        Assert.Equal([new NoteId(1), new NoteId(2)], drag.Picked().OrderBy(note => note.Value));
        Assert.Null(drag.Commit());
    }

    /// <summary>A note off the grid dragged in pitch keeps its time, and one dragged back to where it began sends nothing.</summary>
    [Fact]
    public void ANoteOffTheGridDraggedInPitchKeepsItsTimeAndOneDraggedBackSendsNothing()
    {
        MidiNote off = Note(4, 62, 1.037, 0.3);
        MidiClip clip = Song.Midi("m", 0, 4, off);
        ProjectDocument document = Song.Empty().With(Song.WithMidi("keys", clip));
        NoteDrag drag = NoteDrag.ForMove(document, clip, Geometry, NoteIndex.Of(clip), [off], off, X(1.037) + 2, Y(62));

        drag.Update(X(1.037) + 2 + 1, Y(62) - 24);
        MoveNotes sent = Only<MoveNotes>(drag.Commit());
        Assert.Equal(1.037, sent.Notes[0].Start, 9);
        Assert.Equal(64, sent.Notes[0].Pitch);

        drag.Update(X(1.037) + 2 + 90, Y(62));
        drag.Update(X(1.037) + 2, Y(62));
        Assert.Null(drag.Commit());

        NoteDrag resize = NoteDrag.ForResize(document, clip, Geometry, NoteIndex.Of(clip), [off], off, X(1.337) - 2, Y(62));
        resize.Update(X(1.337) - 2 + 60, Y(62));
        resize.Update(X(1.337) - 2, Y(62));
        Assert.Null(resize.Commit());
    }
}
