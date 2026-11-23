using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>SEAM: bringing a Standard MIDI File into the project as a clip and sending a clip out as one. Not built, and not faked.</summary>
/// <remarks>
/// The server's routes that do the work are in an open pull request (HartsyAI/AudioLab#55): a file is the body of
/// <c>POST /api/projects/{id}/tracks/{track}/midi?start=&amp;name=</c> with the content type <c>audio/midi</c>, and a clip comes out of
/// <c>GET /api/projects/{id}/clips/{clip}/midi</c> as <c>audio/midi</c>. Choosing a file and saving one are browser work (a file input and a
/// download), which needs a script the allowlist does not hold (section 8), so the implementation that lands with the routes keeps them behind
/// this interface, in <c>Interop/</c>. Until then the page registers <see cref="UnavailableMidiTransfer"/>, which refuses with the reason.
/// </remarks>
public interface IMidiTransfer
{
    /// <summary>Why a file cannot be imported or exported now, in one sentence, or null when it can.</summary>
    public string? Unavailable { get; }

    /// <summary>Lets the user choose a .mid file and imports it as a clip on an instrument track, starting at a time on the timeline.</summary>
    /// <param name="track">The instrument track the clip goes on.</param>
    /// <param name="start">Where the clip starts, in seconds.</param>
    /// <param name="cancellationToken">Cancels the choice and the upload.</param>
    /// <returns>The new clip's id in <see cref="EditResult.Created"/>, or the reason the import was refused.</returns>
    public Task<EditResult> ImportAsync(TrackId track, double start, CancellationToken cancellationToken = default);

    /// <summary>Saves a clip of the open project as a .mid file for the user.</summary>
    /// <param name="clip">The MIDI clip to send.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    public Task<EditResult> ExportAsync(ClipId clip, CancellationToken cancellationToken = default);
}
