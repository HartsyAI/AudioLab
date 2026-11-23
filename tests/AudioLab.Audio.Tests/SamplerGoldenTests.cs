using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using static AudioLab.Audio.Tests.Support.SynthProject;

namespace AudioLab.Audio.Tests;

/// <summary>Sampler renders compared with stored ones; regenerate with AUDIOLAB_UPDATE_GOLDEN=1 as GoldenRenderTests says.</summary>
public sealed class SamplerGoldenTests
{
    private const int Rate = 12000;

    // A plucked tone with a noisy start, made from fixed formulas and a fixed generator, so the sample never changes.
    private static PcmAudio Pluck(int channels)
    {
        float[] left = new float[(int)(0.6 * Rate)];
        float[] right = new float[left.Length];
        uint state = 12345;
        for (int i = 0; i < left.Length; i++)
        {
            state = (state * 1664525) + 1013904223;
            double noise = ((state >> 8) / (double)(1 << 24)) - 0.5;
            double t = i / (double)Rate;
            double tone = (0.5 * Math.Sin(2 * Math.PI * 300 * t)) + (0.25 * Math.Sin(2 * Math.PI * 600.5 * t));
            left[i] = (float)((tone * Math.Exp(-4 * t)) + (noise * 0.4 * Math.Exp(-40 * t)));
            right[i] = channels == 1 ? left[i] : (float)((tone * Math.Exp(-5 * t)) - (noise * 0.4 * Math.Exp(-30 * t)));
        }
        return channels == 1 ? new PcmAudio(Rate, [left]) : new PcmAudio(Rate, [left, right]);
    }

    [Fact]
    public void AMelodyPlayedHeldAndAsOneShotsFromAMonoAndAStereoSample()
    {
        SamplerInstrument gated = InstrumentPresets.CreateSampler() with
        {
            Sample = MediaId.From("stereo"),
            SampleName = "stereo",
            RootNote = 60,
            Attack = 0.005,
            Release = 0.15,
            Gain = 0.8,
        };
        SamplerInstrument oneShot = gated with { Sample = MediaId.From("mono"), SampleName = "mono", RootNote = 64, OneShot = true };
        MidiClip melody = Clip(
            "melody",
            0,
            2.5,
            [Note(1, 60, 0, 0.5, 100), Note(2, 64, 0.5, 0.2, 80), Note(3, 67, 1, 0.6, 110), Note(4, 72, 1.5, 0.3, 90), Note(5, 55, 1.9, 0.4, 60)]);
        MidiClip hits = Clip(
            "hits",
            0,
            2.5,
            [Note(1, 64, 0.25, 0.1, 120), Note(2, 52, 0.75, 0.1, 100), Note(3, 76, 1.25, 0.1, 90), Note(4, 40, 1.75, 0.1, 70)]);
        ProjectDocument project = Project(
            InstrumentTrack("a", gated, melody) with { Pan = -0.3 },
            InstrumentTrack("b", oneShot, hits) with { Pan = 0.4 });
        MemoryMedia media = new MemoryMedia().Add("stereo", Pluck(2)).Add("mono", Pluck(1));

        RenderedAudio audio = Render(project, Rate, media: media);

        Assert.True(Peak(audio, 0, 2.5, Rate) > 0.1, "A golden render of silence would hold nothing.");
        GoldenFile.Check("sampler", audio);
    }
}
