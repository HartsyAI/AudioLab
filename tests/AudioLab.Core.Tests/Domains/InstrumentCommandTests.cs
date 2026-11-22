using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class InstrumentCommandTests
{
    private static readonly ClientId _client = ClientId.From("test-client");

    private readonly TrackId _synth = TrackId.From("trk_synth");

    private readonly TrackId _sampler = TrackId.From("trk_sampler");

    private readonly TrackId _audio = TrackId.From("trk_audio");

    private readonly CommandRig _rig;

    public InstrumentCommandTests()
    {
        Track synth = ProjectDefaults.NewTrack(_synth, "Keys", TrackKind.Instrument, "#abc", InstrumentPresets.CreateSynth("Soft Keys"));
        Track sampler = ProjectDefaults.NewTrack(_sampler, "Sampler", TrackKind.Instrument, "#def", InstrumentPresets.CreateSampler());
        _rig = new CommandRig(BlankProject.Create() with { Tracks = [synth, sampler, BlankProject.Track("trk_audio", "Audio")] });
    }

    private SetInstrumentParameter Knob(TrackId track, string name, double value)
    {
        return new SetInstrumentParameter { Project = _rig.Id, Track = track, Parameter = name, Value = value };
    }

    private SetOscillatorWave Wave(TrackId track, int oscillator, OscillatorWave wave)
    {
        return new SetOscillatorWave { Project = _rig.Id, Track = track, Oscillator = oscillator, Wave = wave };
    }

    private SynthInstrument Synth => (SynthInstrument)_rig.Project.Tracks.First(track => track.Id == _synth).Instrument!;

    private SamplerInstrument Sampler => (SamplerInstrument)_rig.Project.Tracks.First(track => track.Id == _sampler).Instrument!;

    // ----- preset

    [Fact]
    public void EveryPresetReplacesTheSoundAndTheSamplerPresetMakesAnEmptySampler()
    {
        foreach (string name in InstrumentPresets.SynthNames)
        {
            _rig.Ok(new SetInstrumentPreset { Project = _rig.Id, Track = _synth, Preset = name });
            Assert.Equal(InstrumentPresets.CreateSynth(name), Synth);
        }

        _rig.Ok(new SetInstrumentPreset { Project = _rig.Id, Track = _synth, Preset = InstrumentPresets.Sampler });

        Assert.Equal(InstrumentPresets.CreateSampler(), _rig.Project.Tracks.First(track => track.Id == _synth).Instrument);
    }

    [Fact]
    public void ApplyingThePresetAgainPutsBackWhatWasTweakedAndAnUnknownPresetIsRefused()
    {
        _rig.Ok(new SetInstrumentParameter { Project = _rig.Id, Track = _synth, Parameter = "cutoff", Value = 500 });

        _rig.Ok(new SetInstrumentPreset { Project = _rig.Id, Track = _synth, Preset = "Soft Keys" });

        Assert.Equal(InstrumentPresets.CreateSynth("Soft Keys"), Synth);
        _rig.RefusedWith(new SetInstrumentPreset { Project = _rig.Id, Track = _synth, Preset = "Choir" }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetInstrumentPreset { Project = _rig.Id, Track = _audio, Preset = "Bell" }, CommandErrorCode.Rejected);
        TrackId none = TrackId.From("trk_none");
        _rig.RefusedWith(new SetInstrumentPreset { Project = _rig.Id, Track = none, Preset = "Choir" }, CommandErrorCode.NotFound);
    }

    // ----- numeric settings

    [Fact]
    public void EverySynthSettingTakesItsMinimumAndMaximumAndRefusesAValueJustOutside()
    {
        foreach (InstrumentParameter setting in InstrumentRules.Parameters(Synth))
        {
            SetInstrumentParameter set = new() { Project = _rig.Id, Track = _synth, Parameter = setting.Name, Value = setting.Min };
            double span = setting.Max - setting.Min;

            _rig.Ok(set);
            Assert.Equal(setting.Min, setting.Get(Synth));
            _rig.Ok(set with { Value = setting.Max });
            Assert.Equal(setting.Max, setting.Get(Synth));
            _rig.RefusedWith(set with { Value = setting.Min - (span / 100) }, CommandErrorCode.Invalid);
            _rig.RefusedWith(set with { Value = setting.Max + (span / 100) }, CommandErrorCode.Invalid);
            _rig.RefusedWith(set with { Value = double.NaN }, CommandErrorCode.Invalid);
        }
    }

    [Fact]
    public void ASamplerHasOnlyAttackReleaseAndGainAndASettingItDoesNotHaveIsRejected()
    {
        foreach (InstrumentParameter setting in InstrumentRules.Parameters(Sampler))
        {
            _rig.Ok(new SetInstrumentParameter { Project = _rig.Id, Track = _sampler, Parameter = setting.Name, Value = setting.Max });
            Assert.Equal(setting.Max, setting.Get(Sampler));
        }

        _rig.RefusedWith(Knob(_sampler, "cutoff", 500), CommandErrorCode.Rejected);
        _rig.RefusedWith(Knob(_synth, "rootNote", 60), CommandErrorCode.Rejected);
        _rig.RefusedWith(Knob(_audio, "gain", 0.5), CommandErrorCode.Rejected);
    }

    [Fact]
    public void ADraggedKnobIsOneGestureAndEveryFrameSucceeds()
    {
        GestureId drag = GestureId.From("knob");
        int steps = _rig.History.UndoCount;
        double before = Synth.Cutoff;

        foreach (double value in new[] { 3000.0, 2500.0, 2000.0, 1500.0 })
        {
            Assert.True(_rig.Run(Knob(_synth, "cutoff", value), drag).Succeeded);
        }
        _rig.History.EndGesture(_client, drag);

        Assert.Equal(steps + 1, _rig.History.UndoCount);
        _rig.History.Undo();
        Assert.Equal(before, Synth.Cutoff);
    }

    // ----- waves, root note, one shot

    [Fact]
    public void EachOscillatorTakesAWaveAndOnlyASynthHasOscillatorsOneOrTwo()
    {
        _rig.Ok(new SetOscillatorWave { Project = _rig.Id, Track = _synth, Oscillator = 1, Wave = OscillatorWave.Square });
        _rig.Ok(new SetOscillatorWave { Project = _rig.Id, Track = _synth, Oscillator = 2, Wave = OscillatorWave.Sawtooth });

        Assert.Equal((OscillatorWave.Square, OscillatorWave.Sawtooth), (Synth.Osc1, Synth.Osc2));
        _rig.RefusedWith(Wave(_synth, 3, OscillatorWave.Sine), CommandErrorCode.Invalid);
        _rig.RefusedWith(Wave(_synth, 1, (OscillatorWave)9), CommandErrorCode.Invalid);
        _rig.RefusedWith(Wave(_sampler, 1, OscillatorWave.Sine), CommandErrorCode.Rejected);
    }

    [Fact]
    public void TheRootNoteIsZeroToOneHundredTwentySevenAndOnlyASamplerHasOne()
    {
        _rig.Ok(new SetSamplerRootNote { Project = _rig.Id, Track = _sampler, Note = 0 });
        _rig.Ok(new SetSamplerRootNote { Project = _rig.Id, Track = _sampler, Note = 127 });
        Assert.Equal(127, Sampler.RootNote);

        _rig.RefusedWith(new SetSamplerRootNote { Project = _rig.Id, Track = _sampler, Note = -1 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetSamplerRootNote { Project = _rig.Id, Track = _sampler, Note = 128 }, CommandErrorCode.Invalid);
        _rig.RefusedWith(new SetSamplerRootNote { Project = _rig.Id, Track = _synth, Note = 60 }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SetSamplerRootNote { Project = _rig.Id, Track = _synth, Note = 999 }, CommandErrorCode.Rejected);
    }

    [Fact]
    public void OneShotIsSwitchedOnASamplerOnly()
    {
        _rig.Ok(new SetSamplerOneShot { Project = _rig.Id, Track = _sampler, OneShot = true });

        Assert.True(Sampler.OneShot);
        _rig.RefusedWith(new SetSamplerOneShot { Project = _rig.Id, Track = _synth, OneShot = true }, CommandErrorCode.Rejected);
        _rig.RefusedWith(new SetSamplerOneShot { Project = _rig.Id, Track = _audio, OneShot = true }, CommandErrorCode.Rejected);
    }

    // ----- sample

    [Fact]
    public void ASampleIsSetWithItsNameClearedAndItsMediaCountsAsReferenced()
    {
        _rig.Ok(new SetSamplerSample { Project = _rig.Id, Track = _sampler, Sample = MediaId.From("med_snare"), SampleName = "Snare" });

        Assert.Equal((MediaId.From("med_snare"), "Snare"), (Sampler.Sample!.Value, Sampler.SampleName));
        Assert.Contains(MediaId.From("med_snare"), ProjectContent.ReferencedMedia(_rig.Project));

        _rig.Ok(new SetSamplerSample { Project = _rig.Id, Track = _sampler });

        Assert.Null(Sampler.Sample);
        Assert.Equal(string.Empty, Sampler.SampleName);
        Assert.DoesNotContain(MediaId.From("med_snare"), ProjectContent.ReferencedMedia(_rig.Project));
    }

    [Fact]
    public void ASampleNameOverTheLimitIsInvalidAndASynthNeedsTheSamplerPresetFirst()
    {
        SetSamplerSample set = new() { Project = _rig.Id, Track = _sampler, SampleName = "x" };

        _rig.RefusedWith(set with { SampleName = new string('n', 201) }, CommandErrorCode.Invalid);
        _rig.RefusedWith(set with { Track = _synth }, CommandErrorCode.Rejected);
        _rig.RefusedWith(set with { Track = TrackId.From("trk_none"), SampleName = new string('n', 201) }, CommandErrorCode.NotFound);
        _rig.Ok(set with { SampleName = new string('n', 200) });
        _rig.Ok(new SetInstrumentPreset { Project = _rig.Id, Track = _synth, Preset = InstrumentPresets.Sampler });
        _rig.Ok(set with { Track = _synth, Sample = MediaId.From("med_1") });
    }
}
