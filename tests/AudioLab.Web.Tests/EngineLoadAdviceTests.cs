using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of the advice about an engine that is not compiled ahead of time.</summary>
public sealed class EngineLoadAdviceTests
{
    private static ProjectDocument With(params EffectKind[] kinds)
    {
        Effect[] effects = [.. kinds.Select((kind, index) => EffectRules.Create(kind, EffectId.From($"e{index}")))];
        return Song.Empty().With(Song.NewTrack("t1") with { Effects = new ValueList<Effect>(effects) });
    }

    /// <summary>A reverb, a delay or a saturator is enough for the warning on the interpreted engine.</summary>
    [Theory]
    [InlineData(EffectKind.Reverb)]
    [InlineData(EffectKind.Delay)]
    [InlineData(EffectKind.Saturation)]
    public void AHeavyEffectIsEnoughForTheWarningOnTheInterpretedEngine(EffectKind kind)
    {
        Assert.Contains("interpreted", EngineLoadAdvice.Warning(With(kind), compiledAhead: false));
        Assert.Null(EngineLoadAdvice.Warning(With(kind), compiledAhead: true));
    }

    /// <summary>A few light effects are fine and many are not.</summary>
    [Fact]
    public void AFewLightEffectsAreFineAndManyAreNot()
    {
        Assert.Null(EngineLoadAdvice.Warning(With(EffectKind.Eq, EffectKind.Compressor), compiledAhead: false));
        EffectKind[] many = [.. Enumerable.Repeat(EffectKind.Eq, 17)];
        Assert.Contains("this many effects", EngineLoadAdvice.Warning(With(many), compiledAhead: false));
    }

    /// <summary>A disabled effect is not built, so it does not count.</summary>
    [Fact]
    public void ADisabledEffectDoesNotCount()
    {
        ProjectDocument document = With(EffectKind.Reverb);
        Track track = document.Tracks[0] with { Effects = [document.Tracks[0].Effects[0] with { Enabled = false }] };

        Assert.Null(EngineLoadAdvice.Warning(document.With(track), compiledAhead: false));
    }
}
