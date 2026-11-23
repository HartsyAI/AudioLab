using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

public sealed class EffectRangeTests
{
    private const int Rate = 8000;

    public static TheoryData<EffectKind, string, bool> Corners()
    {
        TheoryData<EffectKind, string, bool> data = [];
        foreach (EffectKind kind in Enum.GetValues<EffectKind>())
        {
            foreach (EffectParameter setting in EffectRules.Parameters(kind))
            {
                data.Add(kind, setting.Name, false);
                data.Add(kind, setting.Name, true);
            }
        }
        return data;
    }

    // A second of noise and then silence, so that anything still sounding at the end of the render is a tail that did not die.
    private static RenderedAudio Run(Effect effect) => EffectRig.Run(effect, Padded(), tail: true);

    private static PcmAudio Padded()
    {
        float[] noise = [.. Noise.Create(Rate, 1).Channel(0), .. new float[Rate * 3 / 2]];
        return new PcmAudio(Rate, [noise, noise]);
    }

    private static void AssertSane(RenderedAudio audio, EffectKind kind)
    {
        Assert.All(audio.Left, sample => Assert.True(float.IsFinite(sample)));
        Assert.All(audio.Right, sample => Assert.True(float.IsFinite(sample)));
        Assert.True(Signals.Peak(audio.Left) < 50, "the output stays within a sane level");
        double end = Signals.Peak(audio.Left.AsSpan(audio.Frames - 200));
        if (kind == EffectKind.Reverb)
        {
            Assert.True(end < 0.01, "the room has rung out");
        }
        else if (kind == EffectKind.Delay)
        {
            // The tail is cut at 30 s, where a long delay at its highest feedback is still falling.
            Assert.True(end < 0.5 * Signals.Peak(audio.Left), "the echoes are falling");
        }
    }

    [Theory]
    [MemberData(nameof(Corners))]
    public void EverySettingAtItsLowestAndHighestGivesFiniteSaneAudio(EffectKind kind, string name, bool highest)
    {
        Effect effect = EffectRules.Create(kind, EffectId.From("e"));
        EffectParameter setting = EffectRules.Parameters(kind).Single(candidate => candidate.Name == name);

        AssertSane(Run(setting.Set(effect, highest ? setting.Max : setting.Min)), kind);
    }

    [Theory]
    [InlineData(EffectKind.Eq)]
    [InlineData(EffectKind.Compressor)]
    [InlineData(EffectKind.Reverb)]
    [InlineData(EffectKind.Delay)]
    [InlineData(EffectKind.Saturation)]
    public void EverySettingAtItsLowestTogetherAndAtItsHighestTogetherGivesFiniteSaneAudio(EffectKind kind)
    {
        foreach (bool highest in new[] { false, true })
        {
            Effect effect = EffectRules.Create(kind, EffectId.From("e"));
            foreach (EffectParameter setting in EffectRules.Parameters(kind))
            {
                effect = setting.Set(effect, highest ? setting.Max : setting.Min);
            }

            AssertSane(Run(effect), kind);
        }
    }

    [Fact]
    public void ADelayAtItsHighestFeedbackDiesAwayInsteadOfGrowing()
    {
        DelayEffect delay = ((DelayEffect)EffectRules.Create(EffectKind.Delay, EffectId.From("d"))) with
        {
            Time = 0.05,
            Feedback = 0.9,
            Tone = 15000,
            Mix = 1,
        };

        RenderedAudio audio = Run(delay);
        double early = Signals.Rms(audio.Left.AsSpan(Rate, Rate));
        double late = Signals.Rms(audio.Left.AsSpan(audio.Frames - Rate, Rate));

        Assert.True(late < early * 0.1, $"early {early}, late {late}");
    }
}
