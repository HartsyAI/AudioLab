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

    private static MidiClip? Cut(MidiClip clip, double start, double end, bool ripple)
    {
        Assert.True(MidiRangeRules.TryDelete(clip, start, end, ripple, out MidiClip? result));
        return result;
    }

    private static string Notes(MidiClip? clip)
    {
        return string.Join(" ", clip!.Notes.Select(note => $"{note.Id.Value}:{Math.Round(note.Start, 6)}+{Math.Round(note.Duration, 6)}"));
    }

    private static string Pedal(MidiClip? clip)
    {
        return string.Join(" ", clip!.ControlChanges.Where(change => change.Controller == 64).Select(change => $"{change.Time}={change.Value}"));
    }

    [Fact]
    public void ANoteStartingInsideTheRangeGoesAndOneBeforeItStopsAtTheRangeStart()
    {
        MidiClip clip = Clip(0, 10, [Note(1, 1, 1), Note(2, 3, 2), Note(3, 4.5, 0.25), Note(4, 7, 1)]);

        MidiClip? result = Cut(clip, Start, End, false);

        Assert.Equal("1:1+1 2:3+1 4:7+1", Notes(result));
    }

    [Fact]
    public void ANoteGoingOnPastTheRangeEndKeepsThatPartAsANewNoteHeldToTheClipEnd()
    {
        // Note 1 starts in the range and rings to 7; note 2 spans the whole range and rings past the clip end at 9.
        MidiClip clip = Clip(0, 9, [Note(1, 5, 2), Note(2, 2, 20)]);

        MidiClip? result = Cut(clip, Start, End, false);

        Assert.Equal("2:2+2 100:6+1 101:6+3", Notes(result));
        Assert.Equal(102, result!.NextNoteId.Value);
    }

    [Fact]
    public void ANoteEndingWithinAMicrosecondOfTheRangeEndKeepsNoTail()
    {
        MidiClip clip = Clip(0, 9, [Note(1, 5, 1.0000005)]);

        Assert.Equal(string.Empty, Notes(Cut(clip, Start, End, false)));
    }

    [Fact]
    public void ThePedalDownAtTheRangeStartIsReleasedThereAndPressedAgainAtTheEndWhenDownThere()
    {
        MidiClip held = Clip(0, 10, events: [new MidiControlChange(1, 64, 127)]);
        MidiClip upBeforeEnd = Clip(0, 10, events: [new MidiControlChange(1, 64, 127), new MidiControlChange(5, 64, 0)]);
        MidiClip pressedInside = Clip(0, 10, events: [new MidiControlChange(5, 64, 100)]);

        Assert.Equal("1=127 4=0 6=127", Pedal(Cut(held, Start, End, false)));
        Assert.Equal("1=127 4=0", Pedal(Cut(upBeforeEnd, Start, End, false)));
        Assert.Equal("6=100", Pedal(Cut(pressedInside, Start, End, false)));
    }

    [Fact]
    public void ThePedalIsNotPressedAgainWhereAKeptEventAlreadySitsAtTheRangeEndOrTheClipEndsThere()
    {
        MidiClip kept = Clip(0, 10, events: [new MidiControlChange(1, 64, 127), new MidiControlChange(6, 64, 0)]);
        MidiClip endsAtEdge = Clip(0, 6, events: [new MidiControlChange(1, 64, 127)]);

        Assert.Equal("1=127 4=0 6=0", Pedal(Cut(kept, Start, End, false)));
        Assert.Equal("1=127 4=0", Pedal(Cut(endsAtEdge, Start, End, false)));
    }

    [Fact]
    public void OtherControllersInTheRangeGoAndThoseOutsideStay()
    {
        MidiClip clip = Clip(0, 10, events: [new MidiControlChange(1, 1, 10), new MidiControlChange(5, 1, 20), new MidiControlChange(7, 1, 30)]);

        MidiClip? result = Cut(clip, Start, End, false);

        Assert.Equal([1.0, 7.0], result!.ControlChanges.Select(change => change.Time));
    }

    [Fact]
    public void InSlipAClipKeepsItsPlaceAndLengthWhateverItsEdgesDo()
    {
        MidiClip across = Clip(2, 8, [Note(1, 0, 1)]);
        MidiClip startsInside = Clip(5, 4, [Note(1, 2, 1)]);
        MidiClip endsInside = Clip(2, 3.5, [Note(1, 0, 1)]);

        foreach (MidiClip clip in new[] { across, startsInside, endsInside })
        {
            MidiClip? result = Cut(clip, Start, End, false);
            Assert.Equal((clip.Start, clip.Length), (result!.Start, result.Length));
        }
    }

    [Fact]
    public void InShuffleAClipAcrossTheRangeIsShorterByItAndAClipEndingInsideIsCutToItsStart()
    {
        MidiClip across = Clip(2, 8, [Note(1, 0, 1), Note(2, 4, 1)], [new MidiControlChange(5, 1, 9)]);
        MidiClip endsInside = Clip(2, 3.5, [Note(1, 0, 1)]);

        MidiClip? shorter = Cut(across, Start, End, true);
        MidiClip? cut = Cut(endsInside, Start, End, true);

        Assert.Equal((2.0, 6.0), (shorter!.Start, shorter.Length));
        Assert.Equal("1:0+1 2:2+1", Notes(shorter));
        Assert.Equal([3.0], shorter.ControlChanges.Select(change => change.Time));
        Assert.Equal((2.0, 2.0), (cut!.Start, cut.Length));
    }

    [Fact]
    public void InShuffleAClipStartingInsideTheRangeBeginsAtTheRangeStartWithOnlyThePartAfterIt()
    {
        MidiClip clip = Clip(5, 4, [Note(1, 0, 2), Note(2, 2, 1)], [new MidiControlChange(0.5, 64, 127), new MidiControlChange(3, 1, 5)]);

        MidiClip? result = Cut(clip, Start, End, true);

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

        Assert.Equal(6.0, Cut(later, Start, End, true)!.Start);
        Assert.Equal(4.0, Cut(atEnd, Start, End, true)!.Start);
        Assert.Equal(before, Cut(before, Start, End, true));
        Assert.Equal(later, Cut(later, Start, End, false));
    }

    [Fact]
    public void AClipWhollyInsideTheRangeIsGoneAndOneNotTouchedByItComesBackAsItWas()
    {
        Assert.Null(Cut(Clip(4, 2), Start, End, false));
        Assert.Null(Cut(Clip(4.0000005, 1.9999995), Start, End, true));
        MidiClip untouched = Clip(7, 2, [Note(1, 0, 1)], [new MidiControlChange(1, 64, 127)]);
        Assert.Equal(untouched, Cut(untouched, Start, End, false));
    }

    [Fact]
    public void ARepeatedDeleteOfTheSameRangeChangesNothingTheSecondTime()
    {
        MidiClip clip = Clip(0, 10, [Note(1, 0.5, 8)], [new MidiControlChange(1, 64, 127)]);

        MidiClip first = Cut(clip, Start, End, false)!;
        MidiClip second = Cut(first, Start, End, false)!;

        Assert.Equal("1:0.5+3.5 100:6+2.5", Notes(first));
        Assert.Equal("1=127 4=0 6=127", Pedal(first));
        Assert.Equal(101, first.NextNoteId.Value);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ANewNoteKeepsThePitchAndVelocityOfTheNoteItCameFrom()
    {
        MidiClip clip = Clip(0, 10, [new MidiNote(new NoteId(1), 67, 5, 3, 80)]);

        MidiNote tail = Cut(clip, Start, End, false)!.Notes.Single();

        Assert.Equal((new NoteId(100), 67, 6.0, 2.0, 80), (tail.Id, tail.Pitch, tail.Start, tail.Duration, tail.Velocity));
    }

    [Fact]
    public void AClipTheRangeDoesNotTouchComesBackAsItWasEvenWithANoteRingingPastItsEnd()
    {
        MidiClip before = Clip(0, 2, [Note(1, 1.5, 3)], [new MidiControlChange(1, 64, 127)]);

        Assert.Equal(before, Cut(before, 3, 5, false));
        Assert.Equal(before, Cut(before, 3, 5, true));
    }

    [Fact]
    public void AClipAbuttingTheRangeWithinRoundingIsNotCutByIt()
    {
        double abut = 0.1 + 0.7;
        MidiClip after = Clip(abut, 2, [Note(1, 0, 1)], [new MidiControlChange(0, 64, 127)]);
        MidiClip ahead = Clip(0, abut, [Note(1, 0, 1)]);
        MidiClip noteAtEnd = Clip(0, 5, [Note(1, abut, 1)]);

        Assert.Equal(after, Cut(after, 0, 0.8, false));
        Assert.Equal(0.0, Cut(after, 0, 0.8, true)!.Start, 9);
        Assert.Equal(ahead, Cut(ahead, 0.8, 3, false));
        Assert.Equal("1:0.8+1", Notes(Cut(noteAtEnd, 0, 0.8, false)));
    }

    [Fact]
    public void AHeadOrATailUnderTheMinimumNoteLengthIsDroppedAndTakesNoId()
    {
        // Note 1 would keep a head of 0.01 and a tail of 0.005; note 2 a head of 0.03 and a tail of 0.97.
        MidiClip clip = Clip(0, 10, [Note(1, 3.99, 2.015), Note(2, 3.97, 3)]);

        MidiClip? result = Cut(clip, Start, End, false);

        Assert.Equal("2:3.97+0.03 100:6+0.97", Notes(result));
        Assert.Equal(101, result!.NextNoteId.Value);
    }

    [Fact]
    public void ACutThatNeedsMoreNoteIdsThanExistOrMoreItemsThanAClipMayHoldIsNotMade()
    {
        MidiNote[] twoSpanning = [Note(1, 5, 4), Note(2, 5.5, 4)];
        MidiClip noRoom = Clip(0, 10, twoSpanning) with { NextNoteId = new NoteId(NoteId.Max) };
        MidiClip oneRoom = Clip(0, 10, twoSpanning) with { NextNoteId = new NoteId(NoteId.Max - 1) };
        MidiClip exactly = Clip(0, 10, twoSpanning) with { NextNoteId = new NoteId(NoteId.Max - 2) };
        MidiControlChange[] filler = [.. Enumerable.Repeat(new MidiControlChange(0, 1, 0), ProjectLimits.MaxListItems - 1)];
        MidiControlChange[] crowd = [.. filler, new MidiControlChange(1, 64, 127)];

        Assert.False(MidiRangeRules.TryDelete(noRoom, Start, End, false, out MidiClip? refused));
        Assert.Null(refused);
        Assert.False(MidiRangeRules.TryDelete(oneRoom, Start, End, false, out _));
        Assert.True(MidiRangeRules.TryDelete(exactly, Start, End, false, out MidiClip? made));
        Assert.Equal(NoteId.Max, made!.NextNoteId.Value);
        Assert.False(MidiRangeRules.TryDelete(Clip(0, 10, events: crowd), Start, End, false, out _));
        Assert.True(MidiRangeRules.TryDelete(Clip(0, 10, events: filler), Start, End, false, out _));
    }
}
