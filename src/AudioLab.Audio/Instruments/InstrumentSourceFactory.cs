using AudioLab.Core.Model;

namespace AudioLab.Audio.Instruments;

/// <summary>Builds the sound source of each instrument track: the synth of a synth track, nothing yet for a sampler track.</summary>
/// <remarks>Pass it as <see cref="PlanOptions.Sources"/> or <see cref="OfflineOptions.Sources"/>; it is asked once for each track heard.</remarks>
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
            SynthInstrument synth => new SynthSource(
                new SynthSettings(synth, format.SampleRate),
                NoteSchedule.Build(track.MidiClips, format.SampleRate)),
            _ => null,
        };
    }
}
