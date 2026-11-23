using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>Where the notes the user touches in the piano roll are heard: the seam to the sound engine in the page, which is not there yet.</summary>
/// <remarks>
/// The piano roll never makes a sound itself and has no audio path of its own (D5). When the engine host runs in the page, it implements this
/// interface and is registered in place of <see cref="UnavailableAudition"/>; a note is then played by the instrument of the track it is on, so a
/// sampler track plays its sample and a synth track its sound. Until then <see cref="Unavailable"/> says why, and the roll shows it.
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
