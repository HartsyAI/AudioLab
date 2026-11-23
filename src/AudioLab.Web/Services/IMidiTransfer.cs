using AudioLab.Core.Api;
using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>SEAM: bringing a Standard MIDI File into the project as a clip and sending a clip out as one. Not built, and not faked.</summary>
/// <remarks>
/// The server's routes that do the work are in <c>docs/midi.md</c>: a file is the body of
/// <c>POST /api/projects/{id}/tracks/{track}/midi?start=&amp;name=</c> with the content type <c>audio/midi</c>, and a clip comes out of
/// <c>GET /api/projects/{id}/clips/{clip}/midi</c> as <c>audio/midi</c>. Choosing the file is the component's work (the framework's
/// <c>InputFile</c>, as the media library does, D56): it hands the seam a stream, so no script is needed and a service never calls a component.
/// Saving a clip is a link to the GET route, which sends the download headers. Until the typed client of the two routes is written the page
/// registers <see cref="UnavailableMidiTransfer"/>, which says why.
/// </remarks>
public interface IMidiTransfer
{
    /// <summary>Why a file cannot be imported or exported now, in one sentence, or null when it can.</summary>
    public string? Unavailable { get; }

    /// <summary>Sends a .mid file as a new clip on an instrument track, starting at a time on the timeline.</summary>
    /// <param name="project">The open project.</param>
    /// <param name="track">The instrument track the clip goes on.</param>
    /// <param name="name">The clip's name.</param>
    /// <param name="start">Where the clip starts, in seconds.</param>
    /// <param name="file">The file's bytes, which the call reads to the end.</param>
    /// <param name="cancellationToken">Cancels the upload.</param>
    /// <returns>What the server made of it: the new clip, the revision that holds it, and what it left out or did not take over.</returns>
    /// <exception cref="ApiException">The server refused the file; the message says why.</exception>
    public Task<MidiImportResult> ImportAsync(
        ProjectId project,
        TrackId track,
        string name,
        double start,
        Stream file,
        CancellationToken cancellationToken = default);

    /// <summary>The address a clip is saved from as a .mid file, or null when it cannot be (<see cref="Unavailable"/> says why).</summary>
    /// <param name="project">The open project.</param>
    /// <param name="clip">The MIDI clip to send.</param>
    public Uri? ExportAddress(ProjectId project, ClipId clip);
}
