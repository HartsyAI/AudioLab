using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The speech model the lyrics tab asks to time a clip's text or to transcribe its audio.</summary>
/// <remarks>The server runs the model; the page only sends the request and takes the answer.</remarks>
public interface ILyricsModels
{
    /// <summary>Why the models cannot be used now, or null when they can.</summary>
    public string? Unavailable { get; }

    /// <summary>Times a clip's known text against its audio, through the server's aligner.</summary>
    public Task<EditResult> AlignAsync(ClipId clip, string text, CancellationToken cancellationToken = default);

    /// <summary>Writes a clip's lyrics from a transcript of its audio, through the server's speech model.</summary>
    public Task<EditResult> TranscribeAsync(ClipId clip, CancellationToken cancellationToken = default);
}
