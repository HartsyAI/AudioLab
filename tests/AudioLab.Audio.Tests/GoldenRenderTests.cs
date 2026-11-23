using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

/// <summary>Each effect rendered from the same short signal and compared with a stored render within a small tolerance.</summary>
/// <remarks>
/// A change that moves the sound has to be regenerated on purpose: set AUDIOLAB_UPDATE_GOLDEN=1, run these tests (dotnet test -c
/// Release --filter FullyQualifiedName~GoldenRenderTests), look at the diff of the files in Golden, and commit them with the
/// change.
/// </remarks>
public sealed class GoldenRenderTests
{
    private static Effect New(EffectKind kind, string id) => EffectRules.Create(kind, EffectId.From(id));

    private static RenderedAudio Run(Effect effect, params TempoChange[] tempo) =>
        EffectRig.Run(effect, GoldenInput.Create(), tail: true, bpm: 120, tempo);

    [Fact]
    public void Eq() =>
        GoldenFile.Check(
            "eq",
            Run(((EqEffect)New(EffectKind.Eq, "e")) with { HpFreq = 80, LowGain = 6, MidGain = -5, MidFreq = 1100, HighGain = 4, LpFreq = 3000 }));

    [Fact]
    public void Compressor() =>
        GoldenFile.Check(
            "compressor",
            Run(((CompressorEffect)New(EffectKind.Compressor, "c")) with { Threshold = -24, Ratio = 6, Knee = 12, Attack = 0.002, Makeup = 6 }));

    [Fact]
    public void Delay() =>
        GoldenFile.Check(
            "delay",
            Run(
                ((DelayEffect)New(EffectKind.Delay, "d")) with { Sync = true, Division = "1/8", Feedback = 0.5, Tone = 3000, Mix = 0.5 },
                new TempoChange(TempoChangeId.From("tc"), 0.3, 90)));

    [Fact]
    public void Reverb() =>
        GoldenFile.Check(
            "reverb",
            Run(((ReverbEffect)New(EffectKind.Reverb, "r")) with { Size = 0.4, Decay = 3, Predelay = 0.01, Mix = 0.4 }));

    [Fact]
    public void ReverbLongEnoughToUseEveryStageOfTheConvolverAt48Kilohertz()
    {
        ReverbEffect room = ((ReverbEffect)New(EffectKind.Reverb, "r")) with { Size = 0.5, Decay = 3, Predelay = 0.01, Mix = 0.4 };

        GoldenFile.Check("reverb48", EffectRig.Run(room, GoldenInput.Create(48000, 0.1), tail: true));
    }

    [Fact]
    public void Saturation() =>
        GoldenFile.Check(
            "saturation",
            Run(((SaturationEffect)New(EffectKind.Saturation, "s")) with { Drive = 0.7, Trim = -3, Mix = 0.8 }));

    [Fact]
    public void ChainWithABusReverb()
    {
        Track track = TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 0.5)) with
        {
            Pan = -0.4,
            Effects =
            [
                ((EqEffect)New(EffectKind.Eq, "e")) with { LowGain = 3 },
                ((CompressorEffect)New(EffectKind.Compressor, "c")) with { Threshold = -20 },
                ((SaturationEffect)New(EffectKind.Saturation, "s")) with { Drive = 0.4 },
            ],
            Sends = [new Send(TrackId.From("b"), 0.5, false, false)],
        };
        Track bus = TestProject.Track("b", TrackKind.Bus) with
        {
            Effects = [((ReverbEffect)New(EffectKind.Reverb, "r")) with { Size = 0.3, Mix = 1 }],
        };

        RenderedAudio audio = OfflineRenderer.Render(
            TestProject.Create(track, bus),
            new MemoryMedia().Add("m", GoldenInput.Create()),
            new OfflineOptions { SampleRate = GoldenInput.Rate });

        GoldenFile.Check("chain", audio);
    }
}
