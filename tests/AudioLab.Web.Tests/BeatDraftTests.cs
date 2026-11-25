using System.ComponentModel.DataAnnotations;
using System.Reflection;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>The swing and step count typed into the drums tab: only what Core would accept is sent, and a value an agent set is not sent back changed.</summary>
public sealed class BeatDraftTests
{
    /// <summary>A swing from 0 to 1 is read; anything outside, or not a number, is not.</summary>
    [Theory]
    [InlineData("0", true)]
    [InlineData("0.6", true)]
    [InlineData("1", true)]
    [InlineData("1.01", false)]
    [InlineData("-0.1", false)]
    [InlineData("", false)]
    [InlineData("swing", false)]
    public void SwingIsReadOnlyWithinZeroToOne(string text, bool valid)
    {
        Assert.Equal(valid, BeatDraft.TryParseSwing(text, out _));
    }

    /// <summary>A step count from 1 to 256 is read; zero, a fraction, a sign and a count past the limit are not.</summary>
    [Theory]
    [InlineData("1", true)]
    [InlineData("16", true)]
    [InlineData("256", true)]
    [InlineData("0", false)]
    [InlineData("257", false)]
    [InlineData("16.5", false)]
    [InlineData("-4", false)]
    [InlineData("", false)]
    [InlineData("16 steps", false)]
    public void StepsAreWholeNumbersFromOneToTheLimit(string text, bool valid)
    {
        Assert.Equal(valid, BeatDraft.TryParseSteps(text, out _));
    }

    /// <summary>The bounds the draft reads are the bounds of the command that sets them, so the two cannot drift apart.</summary>
    [Fact]
    public void TheBoundsAreTheCommandsOwn()
    {
        RangeAttribute swing = typeof(SetBeatSwing).GetProperty(nameof(SetBeatSwing.Swing))!.GetCustomAttribute<RangeAttribute>()!;
        RangeAttribute steps = typeof(SetBeatSteps).GetProperty(nameof(SetBeatSteps.Steps))!.GetCustomAttribute<RangeAttribute>()!;

        Assert.Equal(0, Convert.ToDouble(swing.Minimum));
        Assert.Equal(1, Convert.ToDouble(swing.Maximum));
        Assert.Equal(1, Convert.ToInt32(steps.Minimum));
        Assert.Equal(ProjectLimits.MaxBeatSteps, Convert.ToInt32(steps.Maximum));
        Assert.True(BeatDraft.TryParseSwing(swing.Maximum.ToString()!, out _));
        Assert.False(BeatDraft.TryParseSteps((ProjectLimits.MaxBeatSteps + 1).ToString(), out _));
    }

    /// <summary>A swing written as its round trip reads back to the same value, so an unedited Set has nothing to send.</summary>
    [Theory]
    [InlineData(0.333)]
    [InlineData(0.1 + 0.2)]
    [InlineData(1.0 / 3.0)]
    public void ASwingSetByAnAgentIsNotChangedByAnUneditedSet(double agentSwing)
    {
        string draft = BeatDraft.FormatSwing(agentSwing);

        Assert.Equal(agentSwing, double.Parse(draft, System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(BeatDraft.CanApplySwing(draft, agentSwing));
        Assert.True(BeatDraft.CanApplySwing("0.7", agentSwing));
    }

    /// <summary>A step count the pattern already has is not sent again; a different one is.</summary>
    [Fact]
    public void SetStepsHasNothingToSendForTheCurrentCount()
    {
        Assert.False(BeatDraft.CanApplySteps("16", 16));
        Assert.True(BeatDraft.CanApplySteps("32", 16));
    }

    /// <summary>A comma decimal is refused and the reason names the dot.</summary>
    [Fact]
    public void ACommaDecimalIsRefusedWithTheDotNamed()
    {
        Assert.False(BeatDraft.TryParseSwing("0,25", out _));
        Assert.Contains("dot", BeatDraft.SwingReason("0,25"), StringComparison.Ordinal);
        Assert.DoesNotContain("dot", BeatDraft.SwingReason("2"), StringComparison.Ordinal);
    }

    /// <summary>Surrounding spaces are read the same way for swing and steps.</summary>
    [Fact]
    public void SurroundingSpacesAreReadTheSameForSwingAndSteps()
    {
        Assert.True(BeatDraft.TryParseSwing(" 0.5 ", out double swing) && swing == 0.5);
        Assert.True(BeatDraft.TryParseSteps(" 16 ", out int steps) && steps == 16);
    }

    /// <summary>The drafts follow the pattern only when its swing or step count changes, not when a lane changes.</summary>
    [Fact]
    public void DraftsFollowOnlyAChangeOfSwingOrSteps()
    {
        BeatPattern pattern = new()
        {
            Steps = 16,
            Swing = 0.5,
            Lanes = [new BeatLane { Id = BeatLaneId.From("bln_1"), Name = "Kick", Gain = 1, Steps = [false] }],
        };

        Assert.False(BeatDraft.ChangedSince(pattern, 0.5, 16));
        Assert.True(BeatDraft.ChangedSince(pattern, 0.7, 16));
        Assert.True(BeatDraft.ChangedSince(pattern, 0.5, 8));
    }
}
