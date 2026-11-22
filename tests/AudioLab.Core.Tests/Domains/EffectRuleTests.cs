using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class EffectRuleTests
{
    private static readonly EffectId _id = EffectId.From("fx_1");

    private static ProjectDocument WithEffect(Effect effect)
    {
        return BlankProject.Create() with { Tracks = [BlankProject.Track("trk_1") with { Effects = [effect] }] };
    }

    [Theory]
    [InlineData(EffectKind.Eq)]
    [InlineData(EffectKind.Compressor)]
    [InlineData(EffectKind.Reverb)]
    [InlineData(EffectKind.Delay)]
    [InlineData(EffectKind.Saturation)]
    public void EveryKindStartsWithValuesTheProjectAcceptsAndKnowsItsKind(EffectKind kind)
    {
        Effect effect = EffectRules.Create(kind, _id);

        Assert.Empty(ProjectValidator.Validate(WithEffect(effect)));
        Assert.Equal(kind, EffectRules.KindOf(effect));
        Assert.True(effect.Enabled);
        Assert.Equal(_id, effect.Id);
    }

    [Fact]
    public void TheStartingValuesAreTheOldDawsDefaults()
    {
        EqEffect eq = Assert.IsType<EqEffect>(EffectRules.Create(EffectKind.Eq, _id));
        CompressorEffect compressor = Assert.IsType<CompressorEffect>(EffectRules.Create(EffectKind.Compressor, _id));
        DelayEffect delay = Assert.IsType<DelayEffect>(EffectRules.Create(EffectKind.Delay, _id));

        Assert.Equal((20.0, 200.0, 1000.0, 1.0, 4000.0, 20000.0), (eq.HpFreq, eq.LowFreq, eq.MidFreq, eq.MidQ, eq.HighFreq, eq.LpFreq));
        Assert.Equal((-24.0, 30.0, 4.0, 0.003, 0.25, 0.0), (compressor.Threshold, compressor.Knee, compressor.Ratio, compressor.Attack, compressor.Release, compressor.Makeup));
        Assert.Equal((0.35, false, "1/4", 0.35, 8000.0, 0.25), (delay.Time, delay.Sync, delay.Division, delay.Feedback, delay.Tone, delay.Mix));
    }

    [Theory]
    [InlineData(EffectKind.Eq)]
    [InlineData(EffectKind.Compressor)]
    [InlineData(EffectKind.Reverb)]
    [InlineData(EffectKind.Delay)]
    [InlineData(EffectKind.Saturation)]
    public void EverySettingsRangeIsExactlyWhatTheValidatorAccepts(EffectKind kind)
    {
        Effect start = EffectRules.Create(kind, _id);

        foreach (EffectParameter setting in EffectRules.Parameters(kind))
        {
            double span = setting.Max - setting.Min;
            Assert.Empty(ProjectValidator.Validate(WithEffect(setting.Set(start, setting.Min))));
            Assert.Empty(ProjectValidator.Validate(WithEffect(setting.Set(start, setting.Max))));
            Assert.NotEmpty(ProjectValidator.Validate(WithEffect(setting.Set(start, setting.Min - (span / 100)))));
            Assert.NotEmpty(ProjectValidator.Validate(WithEffect(setting.Set(start, setting.Max + (span / 100)))));
            Assert.Equal(setting.Max, setting.Get(setting.Set(start, setting.Max)));
        }
    }

    [Fact]
    public void ASettingIsFoundByNameForItsKindOnly()
    {
        Effect compressor = EffectRules.Create(EffectKind.Compressor, _id);

        Assert.NotNull(EffectRules.Find(compressor, "threshold"));
        Assert.Null(EffectRules.Find(compressor, "midFreq"));
        Assert.Null(EffectRules.Find(compressor, "Threshold"));
        Assert.Contains("mix", EffectRules.AllParameterNames);
        Assert.Equal(EffectRules.AllParameterNames.Count, EffectRules.AllParameterNames.Distinct().Count());
    }

    [Theory]
    [InlineData("1/4", 120, 0.5)]
    [InlineData("1/8", 120, 0.25)]
    [InlineData("1/8.", 120, 0.375)]
    [InlineData("1/4.", 60, 1.5)]
    [InlineData("1/2", 30, 4.0)]
    [InlineData("1/16", 0, 0.125)]
    [InlineData("odd", 120, 0.5)]
    public void ASyncedDelayFollowsTheTempoAndNeverPassesFourSeconds(string division, double bpm, double seconds)
    {
        Assert.Equal(seconds, EffectRules.SyncedDelayTime(division, bpm), 9);
    }

    [Fact]
    public void ADelayDivisionThatIsNotOneOfTheSixIsRefusedByTheValidator()
    {
        DelayEffect delay = Assert.IsType<DelayEffect>(EffectRules.Create(EffectKind.Delay, _id));

        Assert.Empty(ProjectValidator.Validate(WithEffect(delay with { Division = "1/8." })));
        Assert.NotEmpty(ProjectValidator.Validate(WithEffect(delay with { Division = "1/3" })));
        Assert.NotEmpty(ProjectValidator.Validate(WithEffect(delay with { Division = string.Empty })));
        Assert.All(DelayEffect.Divisions, division => Assert.Empty(ProjectValidator.Validate(WithEffect(delay with { Division = division }))));
    }

    [Fact]
    public void EveryDivisionIsAKnownOneAndTheValidatorsLimitHolds()
    {
        Assert.All(EffectRules.Divisions, division => Assert.True(EffectRules.IsDivision(division)));
        Assert.All(EffectRules.Divisions, division => Assert.True(division.Length <= 8));
        Assert.False(EffectRules.IsDivision("1/3"));
    }

    [Fact]
    public void EveryPresetOfEveryKindNamesRealSettingsAndMakesAValidEffect()
    {
        foreach (EffectKind kind in Enum.GetValues<EffectKind>())
        {
            Assert.NotEmpty(EffectPresets.For(kind));
            Assert.Equal(EffectPresets.For(kind).Count, EffectPresets.For(kind).Select(preset => preset.Name).Distinct().Count());
            foreach (EffectPreset preset in EffectPresets.For(kind))
            {
                Assert.All(preset.Values.Keys, name => Assert.Contains(EffectRules.Parameters(kind), setting => setting.Name == name));
                Effect applied = EffectPresets.Apply(EffectRules.Create(kind, _id), preset);
                Assert.Empty(ProjectValidator.Validate(WithEffect(applied)));
                Assert.Equal(kind, EffectRules.KindOf(applied));
            }
        }
    }

    [Fact]
    public void APresetLaysItsValuesOverTheDefaultsAndKeepsTheIdAndTheBypass()
    {
        CompressorEffect tweaked = Assert.IsType<CompressorEffect>(EffectRules.Create(EffectKind.Compressor, _id)) with { Enabled = false, Knee = 5, Release = 0.9 };
        EffectPreset preset = EffectPresets.Find(EffectKind.Compressor, "Vocal level")!;

        CompressorEffect applied = Assert.IsType<CompressorEffect>(EffectPresets.Apply(tweaked, preset));

        Assert.Equal((-20.0, 3.0, 0.01, 0.2, 4.0), (applied.Threshold, applied.Ratio, applied.Attack, applied.Release, applied.Makeup));
        Assert.Equal(30, applied.Knee);
        Assert.False(applied.Enabled);
        Assert.Equal(_id, applied.Id);
    }

    [Fact]
    public void ADelayPresetSetsTheSyncAndTheDivisionAndASlapIsFree()
    {
        DelayEffect echo = Assert.IsType<DelayEffect>(EffectPresets.Apply(EffectRules.Create(EffectKind.Delay, _id), EffectPresets.Find(EffectKind.Delay, "Dotted 8th")!));
        DelayEffect slap = Assert.IsType<DelayEffect>(EffectPresets.Apply(echo, EffectPresets.Find(EffectKind.Delay, "Slap")!));

        Assert.Equal((true, "1/8."), (echo.Sync, echo.Division));
        Assert.Equal((false, 0.09), (slap.Sync, slap.Time));
    }

    [Fact]
    public void APresetIsFoundByNameForItsKindOnly()
    {
        Assert.NotNull(EffectPresets.Find(EffectKind.Reverb, "Hall"));
        Assert.Null(EffectPresets.Find(EffectKind.Eq, "Hall"));
        Assert.Contains("Hall", EffectPresets.AllNames);
    }
}
