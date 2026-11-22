using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class SettingsRuleTests
{
    private static ProjectDocument With(Instrument instrument)
    {
        Track track = ProjectDefaults.NewTrack(TrackId.From("trk_1"), "Keys", TrackKind.Instrument, "#abc", instrument);
        return BlankProject.Create() with { Tracks = [track] };
    }

    [Theory]
    [InlineData("Soft Keys")]
    [InlineData("Bell")]
    public void EverySynthSettingsRangeIsExactlyWhatTheValidatorAccepts(string preset)
    {
        SynthInstrument synth = InstrumentPresets.CreateSynth(preset);

        foreach (InstrumentParameter setting in InstrumentRules.Parameters(synth))
        {
            double span = setting.Max - setting.Min;
            Assert.Empty(ProjectValidator.Validate(With(setting.Set(synth, setting.Min))));
            Assert.Empty(ProjectValidator.Validate(With(setting.Set(synth, setting.Max))));
            Assert.NotEmpty(ProjectValidator.Validate(With(setting.Set(synth, setting.Min - (span / 100)))));
            Assert.NotEmpty(ProjectValidator.Validate(With(setting.Set(synth, setting.Max + (span / 100)))));
            Assert.Equal(setting.Max, setting.Get(setting.Set(synth, setting.Max)));
        }
    }

    [Fact]
    public void EverySamplerSettingsRangeIsExactlyWhatTheValidatorAccepts()
    {
        SamplerInstrument sampler = InstrumentPresets.CreateSampler();

        foreach (InstrumentParameter setting in InstrumentRules.Parameters(sampler))
        {
            double span = setting.Max - setting.Min;
            Assert.Empty(ProjectValidator.Validate(With(setting.Set(sampler, setting.Min))));
            Assert.Empty(ProjectValidator.Validate(With(setting.Set(sampler, setting.Max))));
            Assert.NotEmpty(ProjectValidator.Validate(With(setting.Set(sampler, setting.Min - (span / 100)))));
            Assert.NotEmpty(ProjectValidator.Validate(With(setting.Set(sampler, setting.Max + (span / 100)))));
        }
        Assert.Empty(ProjectValidator.Validate(With(sampler with { RootNote = InstrumentRules.LowestNote })));
        Assert.Empty(ProjectValidator.Validate(With(sampler with { RootNote = InstrumentRules.HighestNote })));
        Assert.NotEmpty(ProjectValidator.Validate(With(sampler with { RootNote = InstrumentRules.HighestNote + 1 })));
    }

    [Fact]
    public void TheSettingsAreFoundByNameForTheirKindOnlyAndNoNameRepeatsInAKind()
    {
        SynthInstrument synth = InstrumentPresets.CreateSynth("Soft Keys");
        SamplerInstrument sampler = InstrumentPresets.CreateSampler();

        Assert.NotNull(InstrumentRules.Find(synth, "cutoff"));
        Assert.Null(InstrumentRules.Find(sampler, "cutoff"));
        Assert.NotNull(InstrumentRules.Find(sampler, "release"));
        Assert.Null(InstrumentRules.Find(synth, "Cutoff"));
        Assert.Null(InstrumentRules.Find(synth, "ratio2"));
        Assert.Equal(InstrumentRules.Parameters(synth).Count, InstrumentRules.Parameters(synth).Select(setting => setting.Name).Distinct().Count());
        Assert.Equal(InstrumentRules.AllParameterNames.Count, InstrumentRules.AllParameterNames.Distinct().Count());
        string[] expected = ["mix2", "detune", "cutoff", "resonance", "filterEnv", "attack", "decay", "sustain", "release", "gain"];
        Assert.Equal(expected, InstrumentRules.AllParameterNames);
    }

    [Fact]
    public void EverySynthPresetIsInsideTheRangesOfItsSettings()
    {
        foreach (string name in InstrumentPresets.SynthNames)
        {
            SynthInstrument synth = InstrumentPresets.CreateSynth(name);

            Assert.All(InstrumentRules.Parameters(synth), setting => Assert.InRange(setting.Get(synth), setting.Min, setting.Max));
        }
    }

    [Theory]
    [InlineData("kick", "kick")]
    [InlineData("  a very long sample name that goes on and on  ", "a very long sample name that goes on and")]
    [InlineData("", "")]
    public void ASampleNameIsShownTrimmedAndCutToFortyCharacters(string source, string expected)
    {
        Assert.Equal(expected, InstrumentRules.SampleName(source));
        Assert.True(InstrumentRules.SampleName(source).Length <= InstrumentRules.SampleNameLength);
    }

    [Fact]
    public void OnlyAnAuxBusHasNoInputAndTheMenuHeightsAreInsideTheLimits()
    {
        Assert.True(TrackRules.HasInput(TrackKind.Audio));
        Assert.True(TrackRules.HasInput(TrackKind.Instrument));
        Assert.False(TrackRules.HasInput(TrackKind.Bus));
        Assert.All(TrackRules.HeightPresets, preset => Assert.InRange(preset.Height, TrackRules.MinHeight, TrackRules.MaxHeight));
        Assert.Equal(["Tiny", "Compact", "Small", "Medium", "Large"], TrackRules.HeightPresets.Select(preset => preset.Name));
        Track track = BlankProject.Track("trk_1");
        ProjectDocument Tall(double height) => BlankProject.Create() with { Tracks = [track with { View = track.View with { Height = height } }] };
        Assert.Empty(ProjectValidator.Validate(Tall(TrackRules.MinHeight)));
        Assert.Empty(ProjectValidator.Validate(Tall(TrackRules.MaxHeight)));
        Assert.NotEmpty(ProjectValidator.Validate(Tall(TrackRules.MaxHeight + 1)));
        Assert.NotEmpty(ProjectValidator.Validate(Tall(TrackRules.MinHeight - 1)));
    }

    [Fact]
    public void EveryGridValueIsKnownToTheGridRulesExceptAutoWhichFallsBackToASixteenth()
    {
        TimeSignature meter = new(4, 4);

        Assert.All(GridRules.Values.Where(grid => grid != GridRules.Auto), grid => Assert.True(GridRules.IsKnown(grid), grid));
        Assert.Equal(GridRules.Values.Count, GridRules.Values.Distinct().Count());
        Assert.Equal(GridRules.Auto, GridRules.Values[0]);
        Assert.Equal(GridRules.FallbackBeats, GridRules.Beats(GridRules.Auto, meter));
    }
}
