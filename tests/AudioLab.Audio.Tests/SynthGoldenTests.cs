using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using static AudioLab.Audio.Tests.Support.SynthProject;

namespace AudioLab.Audio.Tests;

/// <summary>Synth renders compared with stored ones; regenerate with AUDIOLAB_UPDATE_GOLDEN=1 as GoldenRenderTests says.</summary>
public sealed class SynthGoldenTests
{
    private const int Rate = 12000;

    private static Track Preset(string name, string id, double pan, params MidiClip[] clips) =>
        InstrumentTrack(id, InstrumentPresets.CreateSynth(name), clips) with { Pan = pan };

    [Fact]
    public void FourPresetsPlayingAChordProgression()
    {
        MidiClip pad = Clip(
            "pad",
            0,
            2,
            [Note(1, 48, 0, 1, 90), Note(2, 55, 0, 1, 90), Note(3, 64, 0, 1, 90),
                Note(4, 50, 1, 1, 90), Note(5, 57, 1, 1, 90), Note(6, 65, 1, 1, 90)]);
        MidiClip keys = Clip(
            "keys",
            0,
            2,
            [Note(1, 72, 0, 0.5, 100), Note(2, 76, 0.5, 0.5, 80), Note(3, 74, 1, 0.5, 100), Note(4, 77, 1.5, 0.5, 60)]);
        MidiClip pluck = Clip("pluck", 0, 2, [.. Enumerable.Range(0, 8).Select(i => Note(i + 1, 60 + ((i % 4) * 4), i * 0.25, 0.2, 70 + (i * 5)))]);
        MidiClip lead = Clip("lead", 0, 2, [Note(1, 79, 0.25, 0.6, 110), Note(2, 81, 1.25, 0.6, 100)]);
        ProjectDocument project = Project(
            Preset("Warm Pad", "pad", -0.3, pad),
            Preset("Soft Keys", "keys", 0.2, keys),
            Preset("Pluck", "pluck", 0.5, pluck),
            Preset("Saw Lead", "lead", -0.6, lead));

        RenderedAudio audio = Render(project, Rate);

        Assert.True(Peak(audio, 0, 2, Rate) > 0.1, "A golden render of silence would hold nothing.");
        GoldenFile.Check("synth-presets", audio);
    }

    [Fact]
    public void ABassAndABellWithTheSustainPedalAndALaterClipOverIt()
    {
        MidiClip bass = Clip("bass", 0, 2, [Note(1, 36, 0, 0.4, 120), Note(2, 43, 0.5, 0.4, 120), Note(3, 36, 1, 0.4, 120)]);
        MidiClip bell = Clip(
            "bell",
            0,
            2,
            [Note(1, 84, 0.1, 0.2, 100), Note(2, 88, 0.6, 0.2, 90), Note(3, 91, 1.1, 0.2, 80)],
            new MidiControlChange(0.05, 64, 127),
            new MidiControlChange(1.8, 64, 0));
        MidiClip over = Clip("over", 1.4, 0.6, [Note(1, 79, 0.1, 0.2, 100)]);
        ProjectDocument project = Project(Preset("Sub Bass", "bass", 0, bass), Preset("Bell", "bell", 0.1, bell, over));

        RenderedAudio audio = Render(project, Rate);

        Assert.True(Peak(audio, 0, 2, Rate) > 0.1, "A golden render of silence would hold nothing.");
        GoldenFile.Check("synth-pedal-layers", audio);
    }
}
