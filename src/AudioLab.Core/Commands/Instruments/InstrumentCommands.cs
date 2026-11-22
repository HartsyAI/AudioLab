using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The instrument commands: the sound of an instrument track, a synth or a sampler.</summary>
internal static class InstrumentCommands
{
    public static void Register(CommandRegistryBuilder builder, InstrumentCommandJsonContext json)
    {
        builder
            .Add("instrument_set_preset", "Gives an instrument track a synth preset or an empty sampler.", json.SetInstrumentPreset, SetPreset)
            .Add("instrument_set_parameter", "Sets one numeric setting of an instrument.", json.SetInstrumentParameter, SetParameter)
            .Add("instrument_set_wave", "Sets the wave of a synth oscillator.", json.SetOscillatorWave, SetWave)
            .Add("instrument_set_root_note", "Sets the note a sampler plays its sample at its own pitch.", json.SetSamplerRootNote, SetRootNote)
            .Add("instrument_set_one_shot", "Sets whether a sampler plays the whole sample.", json.SetSamplerOneShot, SetOneShot)
            .Add("instrument_set_sample", "Sets the media file a sampler plays.", json.SetSamplerSample, SetSample);
    }

    private static CommandResult SetPreset(ProjectDocument document, SetInstrumentPreset command, IIdService ids)
    {
        if (Find(document, command.Track, out Instrument? current) is { } refused)
        {
            return refused;
        }
        if (!InstrumentPresets.Names.Contains(command.Preset))
        {
            return CommandChecks.OutOfRange($"The preset must be one of: {string.Join(", ", InstrumentPresets.Names)}.");
        }
        if (command.Preset == InstrumentPresets.Sampler && current is SamplerInstrument)
        {
            // The old menu item only opened the sampler's panel: a sampler that is already there keeps its sample and settings.
            return CommandResult.Ok(document);
        }
        Instrument instrument = command.Preset == InstrumentPresets.Sampler
            ? InstrumentPresets.CreateSampler()
            : InstrumentPresets.CreateSynth(command.Preset);
        return Change(document, command.Track, _ => instrument);
    }

    private static CommandResult SetParameter(ProjectDocument document, SetInstrumentParameter command, IIdService ids)
    {
        if (Find(document, command.Track, out Instrument? instrument) is { } refused)
        {
            return refused;
        }
        InstrumentParameter? setting = InstrumentRules.Find(instrument!, command.Parameter);
        if (setting is null)
        {
            string kind = instrument is SynthInstrument ? "synth" : "sampler";
            return CommandChecks.Rejected($"A {kind} has no setting named {Shown(command.Parameter)}.");
        }
        return CommandChecks.Range(command.Parameter, command.Value, setting.Min, setting.Max)
            ?? Change(document, command.Track, current => setting.Set(current, command.Value));
    }

    private static CommandResult SetWave(ProjectDocument document, SetOscillatorWave command, IIdService ids)
    {
        if (Find(document, command.Track, out Instrument? instrument) is { } refused)
        {
            return refused;
        }
        if (instrument is not SynthInstrument)
        {
            return CommandChecks.Rejected("Only a synth has oscillators.");
        }
        if (command.Oscillator is not (1 or 2))
        {
            return CommandChecks.OutOfRange("The oscillator is 1 or 2.");
        }
        return Change(document, command.Track, current => command.Oscillator == 1
            ? (SynthInstrument)current with { Osc1 = command.Wave }
            : (SynthInstrument)current with { Osc2 = command.Wave });
    }

    private static CommandResult SetRootNote(ProjectDocument document, SetSamplerRootNote command, IIdService ids)
    {
        return Sampler(document, command.Track)
            ?? CommandChecks.Range(nameof(command.Note), command.Note, InstrumentRules.LowestNote, InstrumentRules.HighestNote)
            ?? Change(document, command.Track, current => (SamplerInstrument)current with { RootNote = command.Note });
    }

    private static CommandResult SetOneShot(ProjectDocument document, SetSamplerOneShot command, IIdService ids)
    {
        return Sampler(document, command.Track)
            ?? Change(document, command.Track, current => (SamplerInstrument)current with { OneShot = command.OneShot });
    }

    private static CommandResult SetSample(ProjectDocument document, SetSamplerSample command, IIdService ids)
    {
        return Sampler(document, command.Track)
            ?? CommandChecks.Length(nameof(command.SampleName), command.SampleName, ProjectLimits.MaxName)
            ?? Change(
                document,
                command.Track,
                current => (SamplerInstrument)current with { Sample = command.Sample, SampleName = command.SampleName ?? string.Empty });
    }

    // A refusal when the track is missing or has no instrument, which only an instrument track has; the instrument comes out when it is there.
    private static CommandResult? Find(ProjectDocument document, TrackId id, out Instrument? instrument)
    {
        instrument = null;
        if (TrackEdits.Exists(document, id) is { } missing)
        {
            return missing;
        }
        instrument = document.Tracks.First(candidate => candidate.Id == id).Instrument;
        return instrument is null ? CommandChecks.Rejected("Only an instrument track has an instrument.") : null;
    }

    private static CommandResult? Sampler(ProjectDocument document, TrackId id)
    {
        if (Find(document, id, out Instrument? instrument) is { } refused)
        {
            return refused;
        }
        return instrument is SamplerInstrument ? null : CommandChecks.Rejected("The track plays a synth; give it the Sampler preset first.");
    }

    private static CommandResult Change(ProjectDocument document, TrackId id, Func<Instrument, Instrument> edit)
    {
        return TrackEdits.Replace(document, id, track => track with { Instrument = edit(track.Instrument!) });
    }

    // A name from a client, cut to a readable length for a message.
    private static string Shown(string name) => ProjectLimits.Shorten(name.Length > 40 ? name[..40] : name);
}
