using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class MidiRangeRuleTests
{
    // The range is 4 to 6 in every test.
    private const double Start = 4;

    private const double End = 6;

    private static MidiClip Clip(double start, double length, MidiNote[]? notes = null, MidiControlChange[]? events = null)
    {
        return new MidiClip
        {
            Id = ClipId.From("m"),
            Name = "m",
            Start = start,
            Length = length,
            Muted = false,
            Notes = [.. notes ?? []],
            NextNoteId = new NoteId(100),
            ControlChanges = [.. events ?? []],
        };
    }

    private static MidiNote Note(int id, double start, double duration) => new(new NoteId(id), 60, start, duration, 100);

    private static string Notes(MidiClip? clip) => string.Join(" ", clip!.Notes.Select(note => $"{note.Id.Value}:{note.Start}+{note.Duration}"));

    private static string Pedal(MidiClip? clip)
    {
        return string.Join(" ", clip!.ControlChanges.Where(change => change.Controller == 64).Select(change => $"{change.Time}={change.Value}"));
    }

    [Fact]
    public void ANoteStartingInsideTheRangeGoesAndOneBeforeItStopsAtTheRangeStart()
    {
        MidiClip clip = Clip(0, 10, [Note(1, 1, 1), Note(2, 3, 2), Note(3, 4.5, 0.25), Note(4, 7, 1)]);

        MidiClip? result = MidiRangeRules.Delete(clip, Start, End, false);

        Assert.Equal("1:1+1 2:3+1 4:7+1", Notes(result));
    }

    [Fact]
    public void ANoteGoingOnPastTheRangeEndKeepsThatPartAsANewNoteHeldToTheClipEnd()
    {
        // Note 1 starts in the range and rings to 7; note 2 spans the whole range and rings past the clip end at 9.
        MidiClip clip = Clip(0, 9, [Note(1, 5, 2), Note(2, 2, 20)]);

        MidiClip? result = MidiRangeRules.Delete(clip, Start, End, false);

        Assert.Equal("2:2+2 100:6+1 101:6+3", Notes(result));
        Assert.Equal(102, result!.NextNoteId.Value);
        Assert.Equal(2, MidiRangeRules.NewNotes(clip, Start, End));
    }

    [Fact]
    public void ANoteEndingWithinAMicrosecondOfTheRangeEndKeepsNoTail()
    {
        MidiClip clip = Clip(0, 9, [Note(1, 5, 1.0000005)]);

        Assert.Equal(string.Empty, Notes(MidiRangeRules.Delete(clip, Start, End, false)));
        Assert.Equal(0, MidiRangeRules.NewNotes(clip, Start, End));
    }

    [Fact]
    public void ThePedalDownAtTheRangeStartIsReleasedThereAndPressedAgainAtTheEndWhenDownThere()
    {
        MidiClip held = Clip(0, 10, events: [new MidiControlChange(1, 64, 127)]);
        MidiClip upBeforeEnd = Clip(0, 10, events: [new MidiControlChange(1, 64, 127), new MidiControlChange(5, 64, 0)]);
        MidiClip pressedInside = Clip(0, 10, events: [new MidiControlChange(5, 64, 100)]);

        Assert.Equal("1=127 4=0 6=127", Pedal(MidiRangeRules.Delete(held, Start, End, false)));
        Assert.Equal("1=127 4=0", Pedal(MidiRangeRules.Delete(upBeforeEnd, Start, End, false)));
        Assert.Equal("6=100", Pedal(MidiRangeRules.Delete(pressedInside, Start, End, false)));
    }

    [Fact]
    public void ThePedalIsNotPressedAgainWhereAKeptEventAlreadySitsAtTheRangeEndOrTheClipEndsThere()
    {
        MidiClip kept = Clip(0, 10, events: [new MidiControlChange(1, 64, 127), new MidiControlChange(6, 64, 0)]);
        MidiClip endsAtEdge = Clip(0, 6, events: [new MidiControlChange(1, 64, 127)]);

        Assert.Equal("1=127 4=0 6=0", Pedal(MidiRangeRules.Delete(kept, Start, End, false)));
        Assert.Equal("1=127 4=0", Pedal(MidiRangeRules.Delete(endsAtEdge, Start, End, false)));
    }

    [Fact]
    public void OtherControllersInTheRangeGoAndThoseOutsideStay()
    {
        MidiClip clip = Clip(0, 10, events: [new MidiControlChange(1, 1, 10), new MidiControlChange(5, 1, 20), new MidiControlChange(7, 1, 30)]);

        MidiClip? result = MidiRangeRules.Delete(clip, Start, End, false);

        Assert.Equal([1.0, 7.0], result!.ControlChanges.Select(change => change.Time));
    }

    [Fact]
    public void InSlipAClipKeepsItsPlaceAndLengthWhateverItsEdgesDo()
    {
        MidiClip across = Clip(2, 8, [Note(1, 0, 1)]);
        MidiClip startsInside = Clip(5, 4, [Note(1, 2, 1)]);
        MidiClip endsInside = Clip(2, 3.5, [Note(1, 0, 1)]);

        Assert.Equal((2.0, 8.0), (MidiRangeRules.Delete(across, Start, End, false)!.Start, MidiRangeRules.Delete(across, Start, End, false)!.Length));
        Assert.Equal((5.0, 4.0), (MidiRangeRules.Delete(startsInside, Start, End, false)!.Start, MidiRangeRules.Delete(startsInside, Start, End, false)!.Length));
        Assert.Equal((2.0, 3.5), (MidiRangeRules.Delete(endsInside, Start, End, false)!.Start, MidiRangeRules.Delete(endsInside, Start, End, false)!.Length));
    }

    [Fact]
    public void InShuffleAClipAcrossTheRangeIsShorterByItAndAClipEndingInsideIsCutToItsStart()
    {
        MidiClip across = Clip(2, 8, [Note(1, 0, 1), Note(2, 4, 1)], [new MidiControlChange(5, 1, 9)]);
        MidiClip endsInside = Clip(2, 3.5, [Note(1, 0, 1)]);

        MidiClip? shorter = MidiRangeRules.Delete(across, Start, End, true);
        MidiClip? cut = MidiRangeRules.Delete(endsInside, Start, End, true);

        Assert.Equal((2.0, 6.0), (shorter!.Start, shorter.Length));
        Assert.Equal("1:0+1 2:2+1", Notes(shorter));
        Assert.Equal([3.0], shorter.ControlChanges.Select(change => change.Time));
        Assert.Equal((2.0, 2.0), (cut!.Start, cut.Length));
    }

    [Fact]
    public void InShuffleAClipStartingInsideTheRangeBeginsAtTheRangeStartWithOnlyThePartAfterIt()
    {
        MidiClip clip = Clip(5, 4, [Note(1, 0, 2), Note(2, 2, 1)], [new MidiControlChange(0.5, 64, 127), new MidiControlChange(3, 1, 5)]);

        MidiClip? result = MidiRangeRules.Delete(clip, Start, End, true);

        Assert.Equal((4.0, 3.0), (result!.Start, result.Length));
        Assert.Equal("2:1+1 100:0+1", Notes(result));
        Assert.Equal([0.0, 2.0], result.ControlChanges.Select(change => change.Time));
        Assert.Equal("0=127", Pedal(result));
    }

    [Fact]
    public void InShuffleALaterClipMovesEarlierAndNeverBelowZeroAndAClipBeforeTheRangeIsUntouched()
    {
        MidiClip later = Clip(8, 2, [Note(1, 0, 1)]);
        MidiClip atEnd = Clip(6, 2);
        MidiClip before = Clip(0, 3, [Note(1, 0, 1)]);

        Assert.Equal(6.0, MidiRangeRules.Delete(later, Start, End, true)!.Start);
        Assert.Equal(4.0, MidiRangeRules.Delete(atEnd, Start, End, true)!.Start);
        Assert.Equal(before, MidiRangeRules.Delete(before, Start, End, true));
        Assert.Equal(later, MidiRangeRules.Delete(later, Start, End, false));
    }

    [Fact]
    public void AClipWhollyInsideTheRangeIsGoneAndOneNotTouchedByItComesBackAsItWas()
    {
        Assert.Null(MidiRangeRules.Delete(Clip(4, 2), Start, End, false));
        Assert.Null(MidiRangeRules.Delete(Clip(4.0000005, 1.9999995), Start, End, true));
        Assert.Equal(0, MidiRangeRules.NewNotes(Clip(4, 2, [Note(1, 0, 20)]), Start, End));
        MidiClip untouched = Clip(7, 2, [Note(1, 0, 1)], [new MidiControlChange(1, 64, 127)]);
        Assert.Equal(untouched, MidiRangeRules.Delete(untouched, Start, End, false));
    }

    [Fact]
    public void ARepeatedDeleteWithNoNoteInTheRangeAddsNothingTheSecondTime()
    {
        MidiClip clip = Clip(0, 10, [Note(1, 0.5, 8)], [new MidiControlChange(1, 64, 127)]);

        MidiClip first = MidiRangeRules.Delete(clip, Start, End, false)!;
        MidiClip second = MidiRangeRules.Delete(first, Start, End, false)!;

        Assert.Equal("1:0.5+3.5 100:6+2.5", Notes(first));
        Assert.Equal("1:0.5+3.5 100:6+2.5", Notes(second));
        Assert.Equal(first.Notes.Count, second.Notes.Count);
    }
}
