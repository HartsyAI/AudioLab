using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>Finding notes for a pointer and for the keyboard.</summary>
public sealed class NoteIndexTests
{
    private static MidiNote Note(int id, int pitch, double start, double duration) => new(new NoteId(id), pitch, start, duration, 90);

    private static NoteIndex Index(params MidiNote[] notes) => NoteIndex.Of(Song.Midi("m", 0, 8, notes));

    /// <summary>A point finds the note under it, the one drawn last when two overlap, and says when it is in the right-hand grip.</summary>
    [Fact]
    public void APointFindsTheNoteUnderItTheLastDrawnOneAndItsGrip()
    {
        NoteIndex index = Index(Note(1, 60, 1, 2), Note(2, 60, 2, 2), Note(3, 62, 1, 1));

        Assert.Equal(new NoteId(1), index.HitTest(1.5, 60, 0.1, out bool edge)!.Value.Id);
        Assert.False(edge);
        Assert.Equal(new NoteId(2), index.HitTest(2.5, 60, 0.1, out _)!.Value.Id);
        Assert.Equal(new NoteId(2), index.HitTest(3.95, 60, 0.1, out edge)!.Value.Id);
        Assert.True(edge);
        Assert.Null(index.HitTest(0.5, 60, 0.1, out _));
        Assert.Null(index.HitTest(1.5, 61, 0.1, out _));
        Assert.Null(index.HitTest(1.5, 200, 0.1, out _));
    }

    /// <summary>The grip never takes more than half of a short note, so a short note can still be moved.</summary>
    [Fact]
    public void TheGripNeverTakesMoreThanHalfOfAShortNote()
    {
        NoteIndex index = Index(Note(1, 60, 1, 0.1));

        index.HitTest(1.02, 60, 5, out bool edge);

        Assert.False(edge);
    }

    /// <summary>The keyboard walks left and right through the notes in time and up and down to the nearest pitch that has one.</summary>
    [Fact]
    public void TheKeyboardWalksInTimeAndByPitch()
    {
        NoteIndex index = Index(Note(1, 60, 0, 1), Note(2, 64, 0.1, 1), Note(3, 72, 3, 1), Note(4, 60, 3.2, 1));
        NoteId first = new(1);

        Assert.Equal(new NoteId(2), index.Walk(first, "ArrowRight"));
        Assert.Null(index.Walk(first, "ArrowLeft"));
        Assert.Equal(new NoteId(2), index.Walk(first, "ArrowUp"));
        Assert.Equal(new NoteId(3), index.Walk(new NoteId(2), "ArrowUp"));
        Assert.Equal(new NoteId(1), index.Walk(new NoteId(2), "ArrowDown"));
        Assert.Equal(new NoteId(1), index.Walk(null, "ArrowRight"));
        Assert.Equal(new NoteId(4), index.Walk(null, "ArrowLeft"));
        Assert.Null(NoteIndex.Of(Song.Midi("m", 0, 4)).Walk(null, "ArrowRight"));
    }

    /// <summary>A rectangle finds every note that touches it.</summary>
    [Fact]
    public void ARectangleFindsEveryNoteThatTouchesIt()
    {
        NoteIndex index = Index(Note(1, 60, 0, 1), Note(2, 64, 2, 1), Note(3, 70, 2, 1));

        Assert.Equal([new NoteId(2)], index.InRectangle(1.9, 2.1, 62, 66));
        Assert.Equal(3, index.InRectangle(0, 8, 0, 127).Count());
        Assert.Equal([new NoteId(1)], index.InRectangle(1, 1.5, 59, 61));
    }
}
