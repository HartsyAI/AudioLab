using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>Where the notes the user touches in the piano roll are heard: the seam to the engine host, which cannot play a live note yet.</summary>
/// <remarks>
/// The piano roll never makes a sound itself and has no audio path of its own (D5). <c>LiveHost</c>, the engine host (D60), implements this
/// interface and is registered in place of <see cref="UnavailableAudition"/> when it can start and stop a note outside a clip: the engine runs
/// in the page and the transport plays the clips of the project (D61), but the synth and the sampler take notes from a clip only. A note is then
/// played by the instrument of the track it is on, so a sampler track plays its sample and a synth track its sound. Until then
/// <see cref="Unavailable"/> says why, and the roll shows it.
/// </remarks>
public interface IAuditionSink
{
    /// <summary>Why a note cannot be heard now, in one sentence, or null when it can.</summary>
    public string? Unavailable { get; }

    /// <summary>Starts a note on the instrument of a track and keeps it sounding until <see cref="NoteOffAsync"/>.</summary>
    /// <param name="track">The instrument track whose sound is used.</param>
    /// <param name="pitch">The MIDI pitch, 0 to 127.</param>
    /// <param name="velocity">The velocity, 1 to 127.</param>
    public Task NoteOnAsync(TrackId track, int pitch, int velocity);

    /// <summary>Lets a note go; a note that is not sounding is ignored.</summary>
    /// <param name="track">The instrument track the note was started on.</param>
    /// <param name="pitch">The MIDI pitch, 0 to 127.</param>
    public Task NoteOffAsync(TrackId track, int pitch);
}
