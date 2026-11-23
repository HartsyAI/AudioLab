using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>The edits a key or a button makes in the piano roll, built from the clip as the page holds it.</summary>
public sealed class RollEditsTests
{
    private static readonly ProjectId _project = ProjectId.From("song");

    private static MidiNote Note(int id, int pitch, double start, double duration, int velocity = 90)
    {
        return new MidiNote(new NoteId(id), pitch, start, duration, velocity);
    }

    private readonly MidiClip _clip = Song.Midi("m", 0, 4, Note(1, 60, 1, 0.5), Note(2, 64, 1.5, 0.5, 120), Note(3, 67, 3, 0.5));

    private ProjectDocument Document => Song.Empty().With(Song.WithMidi("keys", _clip));

    private static IReadOnlyList<Command> Commands(RollEdit? edit) => Assert.IsType<RollEdit>(edit).Build(_project);

    /// <summary>A nudge moves the earliest note to the next grid line and the others with it, and refuses to leave the clip.</summary>
    [Fact]
    public void ANudgeMovesTheNotesByOneGridStepAndStaysInsideTheClip()
    {
        MoveNotes later = Assert.IsType<MoveNotes>(Assert.Single(Commands(RollEdits.Nudge(Document, _clip, [_clip.Notes[0], _clip.Notes[1]], 1))));
        Assert.Equal([1.125, 1.625], later.Notes.Select(target => target.Start).ToArray());
        Assert.Equal([60, 64], later.Notes.Select(target => target.Pitch).ToArray());

        Assert.Null(RollEdits.Nudge(Document, Song.Midi("e", 0, 4, Note(1, 60, 0, 0.5)), [Note(1, 60, 0, 0.5)], -1));
        MoveNotes last = Assert.IsType<MoveNotes>(Assert.Single(Commands(RollEdits.Nudge(Document, _clip, [_clip.Notes[2]], 1))));
        Assert.Equal(3.125, last.Notes[0].Start, 9);
    }

    /// <summary>A stretch changes every length by one step, none below the least length.</summary>
    [Fact]
    public void AStretchChangesEveryLengthByOneStepAndNeverBelowTheLeast()
    {
        RollEdit? grown = RollEdits.Stretch(Document, _clip, [_clip.Notes[0], _clip.Notes[1]], 1);
        ResizeNotes longer = Assert.IsType<ResizeNotes>(Assert.Single(Commands(grown)));
        Assert.All(longer.Notes, length => Assert.Equal(0.625, length.Duration, 9));

        MidiClip tiny = Song.Midi("t", 0, 4, Note(1, 60, 1, 0.05));
        ResizeNotes shorter = Assert.IsType<ResizeNotes>(Assert.Single(Commands(RollEdits.Stretch(Document, tiny, [tiny.Notes[0]], -1))));
        Assert.Equal(0.02, shorter.Notes[0].Duration, 9);
    }

    /// <summary>A change of velocity sends one command for each value the notes land on, in one edit.</summary>
    [Fact]
    public void AChangeOfVelocitySendsOneCommandForEachValueTheNotesLandOn()
    {
        IReadOnlyList<Command> sent = Commands(RollEdits.Louder(_clip, [_clip.Notes[0], _clip.Notes[1], _clip.Notes[2]], 8));

        SetNoteVelocity[] sets = [.. sent.Cast<SetNoteVelocity>().OrderBy(set => set.Velocity)];
        Assert.Equal([98, 127], sets.Select(set => set.Velocity).ToArray());
        Assert.Equal(2, sets[0].Notes!.Count);
        Assert.Null(RollEdits.Louder(Song.Midi("m", 0, 4, Note(1, 60, 1, 0.5, 127)), [Note(1, 60, 1, 0.5, 127)], 8));
    }

    /// <summary>A duplicate goes right after the notes, a paste at the insert point, and notes past the clip are left out with a reason.</summary>
    [Fact]
    public void ADuplicateGoesAfterTheNotesAndNotesPastTheClipAreLeftOutWithAReason()
    {
        RollEdit? duplicate = RollEdits.Duplicate(_clip, [_clip.Notes[0], _clip.Notes[1]], out string? none);
        AddNotes copy = Assert.IsType<AddNotes>(Assert.Single(Commands(duplicate)));
        Assert.Null(none);
        Assert.Equal([2, 2.5], copy.Notes.Select(note => note.Start).ToArray());

        Assert.Null(RollEdits.Duplicate(_clip, [Note(9, 60, 3.6, 0.5)], out string? refusal));
        Assert.NotNull(refusal);
        RollEdit? placed = RollEdits.Place(_clip, [new NewNote(60, 0, 0.5, 90), new NewNote(62, 3, 0.5, 90)], 2, out _);
        AddNotes pasted = Assert.IsType<AddNotes>(Assert.Single(Commands(placed)));
        Assert.Single(pasted.Notes);
        Assert.Equal(2, pasted.Notes[0].Start);
    }

    /// <summary>A note typed at the insert point is one step long and keeps inside the clip.</summary>
    [Fact]
    public void ANoteTypedAtTheInsertPointIsOneStepLong()
    {
        AddNotes typed = Assert.IsType<AddNotes>(Assert.Single(Commands(RollEdits.Type(Document, _clip, 2, 62, 80, out _))));

        Assert.Equal(new NewNote(62, 2, 0.125, 80), typed.Notes[0]);
        Assert.Null(RollEdits.Type(Document, _clip, 4, 62, 80, out string? refusal));
        Assert.NotNull(refusal);
    }

    /// <summary>Transpose and quantize name the notes, or none to mean every note; delete names at least one.</summary>
    [Fact]
    public void TransposeAndQuantizeNameTheNotesOrNoneForAll()
    {
        TransposeNotes all = Assert.IsType<TransposeNotes>(Assert.Single(Commands(RollEdits.Transpose(_clip, new HashSet<NoteId>(), 12))));
        Assert.Null(all.Notes);
        QuantizeNotes some = Assert.IsType<QuantizeNotes>(Assert.Single(Commands(RollEdits.Quantize(_clip, new HashSet<NoteId> { new(2) }))));
        Assert.Equal([new NoteId(2)], some.Notes!);
        Assert.Null(RollEdits.Delete(_clip, new HashSet<NoteId>()));
    }

    /// <summary>The pedal goes down where it is up and up where it is down.</summary>
    [Fact]
    public void ThePedalGoesDownWhereItIsUpAndUpWhereItIsDown()
    {
        MidiClip pedal = _clip with { ControlChanges = [new MidiControlChange(1, 64, 127), new MidiControlChange(2, 64, 0)] };

        Assert.False(RollEdits.PedalDownAt(pedal, 0.5));
        Assert.True(RollEdits.PedalDownAt(pedal, 1.5));
        Assert.False(RollEdits.PedalDownAt(pedal, 2));
        Assert.Equal(127, Assert.IsType<AddControlChange>(Assert.Single(Commands(RollEdits.TogglePedal(pedal, 0.5)))).Value);
        Assert.Equal(0, Assert.IsType<AddControlChange>(Assert.Single(Commands(RollEdits.TogglePedal(pedal, 1.5)))).Value);
        Assert.IsType<RemoveControlChanges>(Assert.Single(Commands(RollEdits.RemovePedal(pedal, 1))));
    }
}
