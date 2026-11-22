using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class NoteRuleTests
{
    private static MidiNote Note(double start, double duration, int pitch = 60, int velocity = 100, int id = 1)
    {
        return new MidiNote(new NoteId(id), pitch, start, duration, velocity);
    }

    [Theory]
    [InlineData(0.13, 0.4, 0.0, 0.5)]
    [InlineData(0.26, 0.2, 0.5, 0.5)]
    [InlineData(0.74, 0.76, 0.5, 1.0)]
    [InlineData(1.0, 1.0, 1.0, 1.0)]
    [InlineData(0.0, 0.01, 0.0, 0.5)]
    [InlineData(0.25, 0.75, 0.5, 1.0)]
    public void QuantizingSnapsTheStartToTheNearestStepAndTheLengthToAWholeStepOfAtLeastOne(double start, double duration, double expectedStart, double expectedDuration)
    {
        MidiNote snapped = NoteRules.Quantize(Note(start, duration), 0.5, 8);

        Assert.Equal(expectedStart, snapped.Start, 9);
        Assert.Equal(expectedDuration, snapped.Duration, 9);
    }

    [Fact]
    public void QuantizingNeverMovesANoteToTheClipEndOrPastIt()
    {
        MidiNote snapped = NoteRules.Quantize(Note(3.9, 0.5), 0.5, 4);

        Assert.Equal(3.5, snapped.Start);
    }

    [Fact]
    public void QuantizingKeepsEverythingButTheStartAndTheLengthAndIgnoresAStepOfNothing()
    {
        MidiNote note = Note(0.3, 0.3, pitch: 72, velocity: 40, id: 9);

        MidiNote snapped = NoteRules.Quantize(note, 0.5, 8);

        Assert.Equal((new NoteId(9), 72, 40), (snapped.Id, snapped.Pitch, snapped.Velocity));
        Assert.Equal(note, NoteRules.Quantize(note, 0, 8));
        Assert.Equal(note, NoteRules.Quantize(note, double.NaN, 8));
    }

    [Fact]
    public void ANoteMovesInTimeAndPitchAndStaysInsideTheClipAndTheRange()
    {
        MidiNote note = Note(1, 0.5, pitch: 60);

        Assert.Equal((1.5, 62), (NoteRules.Move(note, 0.5, 2, 4).Start, NoteRules.Move(note, 0.5, 2, 4).Pitch));
        Assert.Equal(0, NoteRules.Move(note, -5, 0, 4).Start);
        Assert.Equal(NoteRules.LatestStart(4), NoteRules.Move(note, 50, 0, 4).Start);
        Assert.Equal(127, NoteRules.Move(note, 0, 100, 4).Pitch);
        Assert.Equal(0, NoteRules.Move(note, 0, -100, 4).Pitch);
    }

    [Fact]
    public void ANoteIsResizedToAtLeastTheMinimumLength()
    {
        MidiNote note = Note(0, 0.5);

        Assert.Equal(1.0, NoteRules.Resize(note, 0.5).Duration);
        Assert.Equal(NoteRules.MinDuration, NoteRules.Resize(note, -3).Duration);
        Assert.Equal(ProjectLimits.MaxSeconds, NoteRules.Resize(note, 5_000_000).Duration);
    }

    [Fact]
    public void ANewNoteIsOneGridStepLongAndNeverShorterThanTheMinimum()
    {
        Assert.Equal(0.5, NoteRules.NewDuration(0.5));
        Assert.Equal(NoteRules.MinNewDuration, NoteRules.NewDuration(0.01));
    }

    [Theory]
    [InlineData(60, "C4")]
    [InlineData(61, "C#4")]
    [InlineData(21, "A0")]
    [InlineData(0, "C-1")]
    [InlineData(127, "G9")]
    public void PitchesHaveNamesWithOctaves(int pitch, string name)
    {
        Assert.Equal(name, NoteRules.Name(pitch));
    }

    [Theory]
    [InlineData("1/4", 1.0)]
    [InlineData("1/8", 0.5)]
    [InlineData("1/16", 0.25)]
    [InlineData("1/32", 0.125)]
    [InlineData("1/2", 2.0)]
    [InlineData("auto", 0.25)]
    [InlineData("nonsense", 0.25)]
    public void AMusicalGridIsAFractionOfABeatAndAnUnknownOneFallsBackToASixteenth(string grid, double beats)
    {
        Assert.Equal(beats, GridRules.Beats(grid, new TimeSignature(4, 4)));
    }

    [Fact]
    public void ATripletAndABarFollowTheMeterAndASecondsGridHasNoBeats()
    {
        Assert.Equal(1.0 / 3, GridRules.Beats("1/8t", new TimeSignature(4, 4)));
        Assert.Equal(4.0, GridRules.Beats("bar", new TimeSignature(4, 4)));
        Assert.Equal(3.0, GridRules.Beats("bar", new TimeSignature(6, 8)));
        Assert.Null(GridRules.Beats("0.5s", new TimeSignature(4, 4)));
        Assert.True(GridRules.IsKnown("1/8t"));
        Assert.False(GridRules.IsKnown("auto"));
    }

    [Fact]
    public void AGridStepIsInSecondsAtTheTempoWhereItIsAskedFor()
    {
        TempoMap map = TempoMap.From(120, [new TempoChange(TempoChangeId.From("tmp_1"), 10, 60)]);
        TimeSignature signature = new(4, 4);

        Assert.Equal(0.5, GridRules.StepSeconds("1/4", map, signature, 0));
        Assert.Equal(1.0, GridRules.StepSeconds("1/4", map, signature, 12));
        Assert.Equal(2.0, GridRules.StepSeconds("bar", map, signature, 0));
        Assert.Equal(0.1, GridRules.StepSeconds("0.1s", map, signature, 12));
        Assert.Equal(0.01, GridRules.StepSeconds("10ms", map, signature, 0));
    }
}
