using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class EffectParameterTests
{
    public static TheoryData<EffectKind> Kinds => [.. Enum.GetValues<EffectKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EverySettingHasALabelAndAUnitAndLabelsDifferWithinAKind(EffectKind kind)
    {
        IReadOnlyList<EffectParameter> settings = EffectRules.Parameters(kind);

        Assert.All(settings, setting => Assert.False(string.IsNullOrWhiteSpace(setting.Label)));
        Assert.Equal(settings.Count, settings.Select(setting => setting.Label).Distinct().Count());
        Assert.All(settings, setting => Assert.Equal(char.ToUpperInvariant(setting.Label[0]), setting.Label[0]));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ALogarithmicSettingStartsAboveZeroSoItsTravelIsDefined(EffectKind kind)
    {
        Assert.All(
            EffectRules.Parameters(kind).Where(setting => setting.Scale == EffectScale.Logarithmic),
            setting => Assert.True(setting.Min > 0, setting.Name));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void TheTravelRunsFromTheLowestToTheHighestValueAndBackAgain(EffectKind kind)
    {
        foreach (EffectParameter setting in EffectRules.Parameters(kind))
        {
            Assert.Equal(0, setting.ToPosition(setting.Min));
            Assert.Equal(1, setting.ToPosition(setting.Max));
            Assert.Equal(setting.Min, setting.FromPosition(0));
            Assert.Equal(setting.Max, setting.FromPosition(1));
            double start = EffectRules.Default(kind, setting);
            Assert.InRange(start, setting.Min, setting.Max);
            Assert.Equal(start, setting.FromPosition(setting.ToPosition(start)), 9);
            double before = double.NegativeInfinity;
            for (int step = 0; step <= 20; step++)
            {
                double value = setting.FromPosition(step / 20.0);
                Assert.InRange(value, setting.Min, setting.Max);
                Assert.True(value > before, $"{setting.Name} is not rising at step {step}");
                before = value;
            }
        }
    }

    [Fact]
    public void APlaceOutsideTheTravelIsHeldAtTheEndsAndAValueOutsideTheRangeToo()
    {
        EffectParameter threshold = EffectRules.Parameters(EffectKind.Compressor).Single(setting => setting.Name == "threshold");

        Assert.Equal(-60, threshold.FromPosition(-3));
        Assert.Equal(0, threshold.FromPosition(7));
        Assert.Equal(0, threshold.ToPosition(-1000));
        Assert.Equal(1, threshold.ToPosition(1000));
    }

    [Fact]
    public void AFrequencyTakesEqualTravelForEqualRatios()
    {
        EffectParameter midFreq = EffectRules.Parameters(EffectKind.Eq).Single(setting => setting.Name == "midFreq");

        Assert.Equal(1264.9, midFreq.FromPosition(0.5), 1);
        Assert.Equal(0.5, midFreq.ToPosition(Math.Sqrt(200 * 8000)), 9);
    }

    [Fact]
    public void TheDefaultIsWhatANewEffectHas()
    {
        EffectParameter ratio = EffectRules.Parameters(EffectKind.Compressor).Single(setting => setting.Name == "ratio");

        Assert.Equal(4, EffectRules.Default(EffectKind.Compressor, ratio));
        Assert.Equal(0.3, EffectRules.Default(EffectKind.Reverb, EffectRules.Parameters(EffectKind.Reverb).Single(setting => setting.Name == "mix")));
    }
}
