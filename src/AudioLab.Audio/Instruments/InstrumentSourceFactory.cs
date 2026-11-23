using AudioLab.Core.Model;

namespace AudioLab.Audio.Instruments;

/// <summary>Builds the sound source of each instrument track: the synth of a synth track and the sampler of a sampler track.</summary>
/// <remarks>
/// Pass it as <see cref="PlanOptions.Sources"/> or <see cref="OfflineOptions.Sources"/>; it is asked once for each track heard. A sampler
/// with no sample, or whose track has no note that would sound, is left silent. A sampler that would sound asks the plan's media for its
/// sample, so a sample the media cannot supply fails the plan with <see cref="MissingMediaException"/> as a missing clip does.
/// </remarks>
public sealed class InstrumentSourceFactory : ITrackSourceFactory
{
    /// <inheritdoc />
    public ITrackSource? Create(ProjectDocument document, Track track, AudioFormat format, IPlanMedia media)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(media);
        return track.Instrument switch
        {
            SynthInstrument synth => Synth(synth, track, format),
            SamplerInstrument sampler => Sampler(sampler, track, format, media),
            _ => null,
        };
    }

    private static PolyphonicSource Synth(SynthInstrument synth, Track track, AudioFormat format)
    {
        SynthSettings settings = new(synth, format.SampleRate);
        NoteEvent[] notes = NoteSchedule.Build(track.MidiClips, format.SampleRate);
        return new PolyphonicSource(() => new SynthVoice(settings), notes, mono: true, settings.TailSeconds);
    }

    private static PolyphonicSource? Sampler(SamplerInstrument sampler, Track track, AudioFormat format, IPlanMedia media)
    {
        if (sampler.Sample is not { } id)
        {
            return null;
        }
        NoteEvent[] notes = NoteSchedule.Build(track.MidiClips, format.SampleRate);
        if (notes.Length == 0)
        {
            return null;
        }
        PcmAudio? sample = media.Get(id);
        if (sample is null)
        {
            return null;
        }
        if (sample.SampleRate != format.SampleRate)
        {
            throw new InvalidOperationException($"The sample {id} is at {sample.SampleRate} Hz and the plan at {format.SampleRate} Hz.");
        }
        SamplerSettings settings = new(sampler, sample);
        double trackEnd = track.MidiClips.Where(clip => !clip.Muted).Select(clip => clip.Start + clip.Length).DefaultIfEmpty(0).Max();
        return new PolyphonicSource(() => new SamplerVoice(settings), notes, sample.ChannelCount == 1, settings.TailSeconds(notes, trackEnd));
    }
}
