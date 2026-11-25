using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The lyrics models until the server serves them: nothing is faked, and each request says why it cannot be made.</summary>
public sealed class UnavailableLyricsModels : ILyricsModels
{
    private const string Reason = "Needs the speech model, which is not built yet.";

    /// <inheritdoc />
    public string? Unavailable => Reason;

    /// <inheritdoc />
    public Task<EditResult> AlignAsync(ClipId clip, string text, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EditResult.Refused(Reason));
    }

    /// <inheritdoc />
    public Task<EditResult> TranscribeAsync(ClipId clip, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EditResult.Refused(Reason));
    }
}
