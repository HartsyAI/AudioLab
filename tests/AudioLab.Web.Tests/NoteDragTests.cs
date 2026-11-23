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

        MoveNotes sent = Only<MoveNotes>(drag.Commit(_clip));
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

        MoveNotes sent = Only<MoveNotes>(drag.Commit(_clip));
        Assert.Equal(3.98, sent.Notes[1].Start, 9);
        Assert.Equal(1.98, sent.Notes[0].Start, 9);
        drag.Update(X(1) + 2, Y(60));
        Assert.Null(drag.Commit(_clip));
    }

    /// <summary>A press that never travels three pixels is a click and sends nothing.</summary>
    [Fact]
    public void APressThatNeverTravelsIsAClick()
    {
        NoteDrag drag = Move([_clip.Notes[0]], _clip.Notes[0]);

        drag.Update(X(1) + 3, Y(60) + 1);

        Assert.False(drag.Active);
        Assert.Null(drag.Commit(_clip));
    }

    /// <summary>A resize puts the grabbed edge on the grid and every selected note grows by the same amount, none below the least length.</summary>
    [Fact]
    public void AResizeMovesTheEdgeToTheGridAndEveryNoteGrowsByTheSameAmount()
    {
        MidiNote[] selected = [_clip.Notes[0], _clip.Notes[1]];
        NoteDrag drag = NoteDrag.ForResize(Document, _clip, Geometry, NoteIndex.Of(_clip), selected, selected[0], X(1.5) - 2, Y(60));

        drag.Update(X(1.5) - 2 + 21, Y(60));

        ResizeNotes sent = Only<ResizeNotes>(drag.Commit(_clip));
        Assert.Equal(0.75, sent.Notes[0].Duration, 9);
        Assert.Equal(0.75, sent.Notes[1].Duration, 9);
        drag.Update(X(1.5) - 2 - 500, Y(60));
        Assert.Equal(0.02, Only<ResizeNotes>(drag.Commit(_clip)).Notes[0].Duration, 9);
    }

    /// <summary>A drawn note starts in the cell that was pressed, is at least one step long and goes in with the velocity asked for.</summary>
    [Fact]
    public void ADrawnNoteStartsInThePressedCellAndIsAtLeastOneStepLong()
    {
        NoteDrag drag = NoteDrag.ForDraw(Document, _clip, Geometry, NoteIndex.Of(_clip), 77, X(2.06), Y(70));

        AddNotes tap = Only<AddNotes>(drag.Commit(_clip));
        Assert.Equal(new NewNote(70, 2, 0.125, 77), tap.Notes[0]);
        drag.Update(X(2.06) + 40, Y(70));
        AddNotes dragged = Only<AddNotes>(drag.Commit(_clip));
        Assert.Equal(0.5, dragged.Notes[0].Duration ?? 0, 9);
        NoteDrag late = NoteDrag.ForDraw(Document, _clip, Geometry, NoteIndex.Of(_clip), 100, X(3.99), Y(60));
        Assert.Equal(3.875, Only<AddNotes>(late.Commit(_clip)).Notes[0].Start, 9);
    }

    /// <summary>An erase removes the notes the pointer crossed.</summary>
    [Fact]
    public void AnEraseRemovesTheNotesThePointerCrossed()
    {
        NoteDrag drag = NoteDrag.ForErase(Document, _clip, Geometry, NoteIndex.Of(_clip), X(1.1), Y(60));

        drag.Update(X(1.7), Y(64));
        drag.Update(X(1.8), Y(80));

        DeleteNotes sent = Only<DeleteNotes>(drag.Commit(_clip));
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
        Assert.Null(drag.Commit(_clip));
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
        MoveNotes sent = Only<MoveNotes>(drag.Commit(clip));
        Assert.Equal(1.037, sent.Notes[0].Start, 9);
        Assert.Equal(64, sent.Notes[0].Pitch);

        drag.Update(X(1.037) + 2 + 90, Y(62));
        drag.Update(X(1.037) + 2, Y(62));
        Assert.Null(drag.Commit(clip));

        NoteDrag resize = NoteDrag.ForResize(document, clip, Geometry, NoteIndex.Of(clip), [off], off, X(1.337) - 2, Y(62));
        resize.Update(X(1.337) - 2 + 60, Y(62));
        resize.Update(X(1.337) - 2, Y(62));
        Assert.Null(resize.Commit(clip));
    }

    /// <summary>A fast stroke of the erase takes every note on the line between two samples, not only the notes under the samples.</summary>
    [Fact]
    public void AFastEraseStrokeTakesEveryNoteBetweenTwoSamples()
    {
        MidiClip row = Song.Midi("m", 0, 4, Note(1, 60, 0.2, 0.2), Note(2, 60, 1, 0.2), Note(3, 60, 2, 0.2), Note(4, 60, 3, 0.2));
        ProjectDocument document = Song.Empty().With(Song.WithMidi("keys", row));
        NoteDrag drag = NoteDrag.ForErase(document, row, Geometry, NoteIndex.Of(row), X(0.3), Y(60));

        drag.Update(X(3.1), Y(60));

        Assert.Equal([1, 2, 3, 4], Only<DeleteNotes>(drag.Commit(row)).Notes.Select(note => note.Value).Order().ToArray());
        MidiClip stairs = Song.Midi("m", 0, 4, Note(1, 60, 1, 0.3), Note(2, 62, 2, 0.3), Note(3, 64, 3, 0.3), Note(4, 72, 2, 0.3));
        NoteDrag diagonal = NoteDrag.ForErase(document, stairs, Geometry, NoteIndex.Of(stairs), X(0.9), Y(60));
        diagonal.Update(X(3.1), Y(64));
        Assert.Equal([1, 2, 3], diagonal.Erased.Select(note => note.Value).Order().ToArray());
    }

    /// <summary>On release the targets are made again from the clip as it is then: a note that is gone is left out and counted.</summary>
    [Fact]
    public void OnReleaseTheDragIsRebasedOntoTheClipAsItIsNow()
    {
        MidiNote[] selected = [_clip.Notes[0], _clip.Notes[1]];
        NoteDrag drag = Move(selected, selected[0]);
        drag.Update(X(1) + 2 + 33, Y(60) - 24);
        MidiClip other = _clip with { Notes = [_clip.Notes[1] with { Start = 2.25 }, _clip.Notes[2]] };

        MoveNotes sent = Only<MoveNotes>(drag.Commit(other));

        NoteTarget target = Assert.Single(sent.Notes);
        Assert.Equal(new NoteId(2), target.Note);
        Assert.Equal(2.25 + 0.375, target.Start, 9);
        Assert.Equal(1, drag.Lost);
        MidiClip none = _clip with { Notes = [_clip.Notes[2]] };
        Assert.Null(drag.Commit(none));
        Assert.Equal(2, drag.Lost);
    }

    /// <summary>A resize follows the server's rule: a note may be made to ring past the end, and one that already does can be lengthened.</summary>
    [Fact]
    public void AResizeFollowsTheServersRuleAndHasNoClipEnd()
    {
        MidiNote past = Note(7, 60, 3.9, 1);
        MidiClip clip = Song.Midi("m", 0, 4, past);
        ProjectDocument document = Song.Empty().With(Song.WithMidi("keys", clip));
        NoteDrag drag = NoteDrag.ForResize(document, clip, Geometry, NoteIndex.Of(clip), [past], past, X(4.9) - 2, Y(60));

        drag.Update(X(4.9) - 2 + 40, Y(60));

        Assert.Equal(1.475, Only<ResizeNotes>(drag.Commit(clip)).Notes[0].Duration, 9);
    }

    /// <summary>A move of more notes than one command carries is several commands.</summary>
    [Fact]
    public void AMoveOfMoreNotesThanOneCommandCarriesIsSeveralCommands()
    {
        MidiNote[] many = [.. Enumerable.Range(1, RollEdit.ChunkSize + 5).Select(id => Note(id, 60, 0.5 + (id * 0.0001), 0.01))];
        MidiClip clip = Song.Midi("m", 0, 8, many);
        ProjectDocument document = Song.Empty().With(Song.WithMidi("keys", clip));
        NoteDrag drag = NoteDrag.ForMove(document, clip, new RollGeometry(80, 12, 8), NoteIndex.Of(clip), many, many[0], X(0.5), Y(60));

        drag.Update(X(0.5) + 40, Y(60));

        IReadOnlyList<Command> sent = Assert.IsType<RollEdit>(drag.Commit(clip)).Build(_project);
        Assert.Equal([RollEdit.ChunkSize, 5], sent.Cast<MoveNotes>().Select(move => move.Notes.Count).ToArray());
    }
}
