using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class BeatRuleTests
{
    [Theory]
    [InlineData(120, 0.125)]
    [InlineData(60, 0.25)]
    [InlineData(240, 0.0625)]
    public void OneStepIsASixteenthNoteAtTheTempo(double bpm, double seconds)
    {
        Assert.Equal(seconds, BeatRules.StepSeconds(bpm), 12);
    }

    [Fact]
    public void ATempoOutsideTheProjectsRangeIsHeldInsideItSoAStepIsNeverInfinite()
    {
        Assert.Equal(60.0 / ProjectLimits.MinBpm / 4, BeatRules.StepSeconds(0));
        Assert.Equal(60.0 / ProjectLimits.MinBpm / 4, BeatRules.StepSeconds(-5));
        Assert.Equal(60.0 / ProjectLimits.MaxBpm / 4, BeatRules.StepSeconds(1e9));
    }

    [Fact]
    public void OddStepsAreLateBySwingTimesAStepAndEvenStepsStayOnTheGrid()
    {
        double step = BeatRules.StepSeconds(120);

        Assert.Equal(0, BeatRules.StepStart(0, step, 0.5));
        Assert.Equal(step * 1.5, BeatRules.StepStart(1, step, 0.5), 12);
        Assert.Equal(step * 2, BeatRules.StepStart(2, step, 0.5), 12);
        Assert.Equal(step * 3, BeatRules.StepStart(3, step, 0), 12);
        Assert.Equal(step * 4, BeatRules.StepStart(3, step, 1), 12);
        Assert.Equal(2.0, BeatRules.PatternSeconds(16, step), 12);
    }

    [Theory]
    [InlineData("kick", "kick")]
    [InlineData("  snare drum  ", "snare drum")]
    [InlineData("a very long sample file name", "a very long sample")]
    [InlineData("", "")]
    public void APadIsNamedAfterItsSourceTrimmedAndCutToEighteenCharacters(string source, string expected)
    {
        Assert.Equal(expected, BeatRules.PadName(source));
        Assert.True(BeatRules.PadName(source).Length <= BeatRules.PadNameLength);
    }

    [Fact]
    public void ResizingKeepsTheLeadingStepsAndNewOnesAreOff()
    {
        ValueList<bool> steps = [true, false, true, true];

        Assert.Equal([true, false, true, true, false, false], BeatRules.Resize(steps, 6));
        Assert.Equal([true, false], BeatRules.Resize(steps, 2));
        Assert.Equal(steps, BeatRules.Resize(steps, 4));
    }

    [Fact]
    public void AProjectWithOnlyADrumPadHasContentAndItsSampleIsReferenced()
    {
        BeatLane pad = new() { Id = BeatLaneId.From("bln_1"), Name = "Kick", Media = MediaId.From("med_kick"), Gain = 1, Steps = [false, false] };
        ProjectDocument empty = BlankProject.Create();
        ProjectDocument padded = empty with { BeatPattern = new BeatPattern { Steps = 2, Swing = 0, Lanes = [pad] } };

        Assert.False(ProjectContent.HasContent(empty));
        Assert.True(ProjectContent.HasContent(padded));
        Assert.Empty(ProjectContent.ReferencedMedia(empty));
        Assert.Equal([MediaId.From("med_kick")], ProjectContent.ReferencedMedia(padded));
    }

    [Fact]
    public void EverySampleTheSampleProjectPlaysIsReferencedFromItsClipsItsPadsOrItsSampler()
    {
        ProjectDocument sample = SampleProject.Create(UserId.Local);
        HashSet<MediaId> expected =
        [
            .. sample.Tracks.SelectMany(track => track.Clips).Select(clip => clip.Media),
            .. sample.BeatPattern.Lanes.Where(lane => lane.Media is not null).Select(lane => lane.Media!.Value),
        ];

        IReadOnlySet<MediaId> referenced = ProjectContent.ReferencedMedia(sample);

        Assert.True(expected.IsSubsetOf(referenced));
        Assert.Contains(sample.BeatPattern.Lanes[0].Media!.Value, referenced);
        Assert.Contains(sample.Tracks.Select(track => track.Instrument).OfType<SamplerInstrument>().First().Sample!.Value, referenced);
        Assert.True(ProjectContent.HasContent(sample));
    }
}
