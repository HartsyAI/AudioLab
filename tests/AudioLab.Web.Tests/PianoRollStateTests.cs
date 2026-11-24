using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>What the roll of one client holds: selection, the insert point and the clipboard.</summary>
public sealed class PianoRollStateTests
{
    private static MidiClip Clip(string id) => Song.Midi(
        id,
        0,
        4,
        new MidiNote(new NoteId(1), 60, 1, 0.5, 90),
        new MidiNote(new NoteId(2), 64, 2, 0.5, 90));

    /// <summary>Another clip starts over; the same clip keeps the selection, and a note that is gone leaves it.</summary>
    [Fact]
    public void AnotherClipStartsOverAndANoteThatIsGoneLeavesTheSelection()
    {
        PianoRollState state = new();
        MidiClip clip = Clip("a");
        state.Bind(clip.Id);
        state.SelectAll(clip);
        state.MoveInsert(2);
        state.Bind(clip.Id);
        Assert.Equal(2, state.Selected.Count);

        state.Prune(clip with { Notes = [clip.Notes[0]] });
        Assert.Equal([new NoteId(1)], state.Selected);
        state.Bind(ClipId.From("b"));
        Assert.Empty(state.Selected);
        Assert.Equal(0, state.InsertTime);
    }

    /// <summary>Selecting changes the version, a toggle takes a note out, and the pedal selection excludes notes.</summary>
    [Fact]
    public void SelectingChangesTheVersionAndAToggleTakesANoteOut()
    {
        PianoRollState state = new();
        int version = state.Version;

        state.Select([new NoteId(1), new NoteId(2)]);
        state.Toggle(new NoteId(1));
        Assert.Equal([new NoteId(2)], state.Selected);
        Assert.True(state.Version > version);
        state.SelectPedal(1.5);
        Assert.Empty(state.Selected);
        Assert.Equal(1.5, state.SelectedPedal);
    }

    /// <summary>A copy keeps the starts counted from the first note, in time order.</summary>
    [Fact]
    public void ACopyKeepsTheStartsCountedFromTheFirstNote()
    {
        PianoRollState state = new();
        MidiClip clip = Clip("a");

        state.Copy([clip.Notes[1], clip.Notes[0]]);

        Assert.Equal([0, 1], state.Clipboard.Select(note => note.Start).ToArray());
        Assert.Equal([60, 64], state.Clipboard.Select(note => note.Pitch).ToArray());
    }

    /// <summary>The zoom stays between the coarsest and the finest scale.</summary>
    [Fact]
    public void TheZoomStaysBetweenTheCoarsestAndTheFinestScale()
    {
        PianoRollState state = new();

        for (int step = 0; step < 12; step++)
        {
            state.Zoom(zoomIn: true);
        }
        Assert.Equal(RollGeometry.MaxPixelsPerSecond, state.PixelsPerSecond);
        for (int step = 0; step < 12; step++)
        {
            state.Zoom(zoomIn: false);
        }
        Assert.Equal(RollGeometry.MinPixelsPerSecond, state.PixelsPerSecond);
    }
}
