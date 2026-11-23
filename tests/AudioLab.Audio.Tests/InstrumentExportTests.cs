using AudioLab.Audio.Instruments;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using static AudioLab.Audio.Tests.Support.SynthProject;

namespace AudioLab.Audio.Tests;

/// <summary>Instrument tracks reach the mix, the stems and the WAV file through the same factory (D5, D40).</summary>
public sealed class InstrumentExportTests
{
    private const int Rate = Rendering.Rate;

    private static readonly MemoryMedia _media = new MemoryMedia().Add("smp", Signals.Sine(330, 1, Rate, 0.5, 2));

    private static ProjectDocument Project() =>
        SynthProject.Project(
            InstrumentTrack("syn", InstrumentPresets.CreateSynth("Soft Keys"), Clip("a", 0, 2, [Note(1, 60, 0, 1), Note(2, 67, 0.5, 1)])) with
            {
                Sends = [new Send(TrackId.From("rev"), 0.5, false, false)],
                Pan = -0.4,
            },
            InstrumentTrack(
                "smp",
                InstrumentPresets.CreateSampler() with { Sample = MediaId.From("smp"), SampleName = "smp", RootNote = 64, Release = 0.3 },
                Clip("b", 0, 2, [Note(1, 64, 0.25, 0.5), Note(2, 71, 1, 0.5)])),
            TestProject.Track("rev", TrackKind.Bus) with
            {
                Effects = [((ReverbEffect)EffectRules.Create(EffectKind.Reverb, EffectId.From("r"))) with { Mix = 1 }],
            });

    private static OfflineOptions Options() => new() { SampleRate = Rate, Sources = new InstrumentSourceFactory() };

    [Fact]
    public void AnInstrumentTrackWithAnUnmutedMidiClipHasAStemAndOneWithOnlyMutedClipsHasNone()
    {
        ProjectDocument project = Project();
        Track muted = project.Tracks[1] with { MidiClips = [project.Tracks[1].MidiClips[0] with { Muted = true }] };

        Assert.Equal(["syn", "smp", "rev"], StemExporter.StemTracks(project).Select(id => id.Value));
        Assert.Equal(["syn", "rev"], StemExporter.StemTracks(project with { Tracks = [project.Tracks[0], muted, project.Tracks[2]] }).Select(id => id.Value));
    }

    [Fact]
    public void TheStemsOfSynthAndSamplerTracksAndTheirBusAddUpToTheMix()
    {
        ProjectDocument project = Project();
        RenderedAudio mix = OfflineRenderer.Render(project, _media, Options());

        IReadOnlyList<Stem> stems = StemExporter.Render(project, _media, Options());

        Assert.Equal(3, stems.Count);
        Assert.All(stems, stem => Assert.True(stem.Audio.Left.Max(Math.Abs) > 0.01f));
        Assert.Equal(mix.Frames, stems[0].Audio.Frames);
        for (int i = 0; i < mix.Frames; i++)
        {
            Assert.Equal(mix.Left[i], stems.Sum(stem => stem.Audio.Left[i]), 1e-6);
            Assert.Equal(mix.Right[i], stems.Sum(stem => stem.Audio.Right[i]), 1e-6);
        }
    }

    [Fact]
    public void AStreamedWavOfInstrumentTracksIsTheWavOfTheRenderAndHoldsSound()
    {
        ProjectDocument project = Project();
        WavOptions wav = new() { Format = WavFormat.Float32 };
        using MemoryStream streamed = new();
        using MemoryStream buffered = new();

        WavExporter.Write(project, _media, streamed, wav, Options());
        RenderedAudio audio = OfflineRenderer.Render(project, _media, Options());
        WavExporter.Write(audio, buffered, wav);

        Assert.Equal(buffered.ToArray(), streamed.ToArray());
        Assert.True(audio.Left.Max(Math.Abs) > 0.05f);
    }

    [Fact]
    public void AMissingSampleFailsAnExportBeforeAnyByteIsWritten()
    {
        using MemoryStream output = new();

        Assert.Throws<MissingMediaException>(() => WavExporter.Write(Project(), new MemoryMedia(), output, new WavOptions(), Options()));

        Assert.Equal(0, output.Length);
    }
}
