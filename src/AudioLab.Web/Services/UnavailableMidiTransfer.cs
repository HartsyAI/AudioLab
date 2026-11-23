using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The transfer the page has until the server MIDI routes and a file picker are in: every call is refused with the reason.</summary>
public sealed class UnavailableMidiTransfer : IMidiTransfer
{
    private const string Reason = "Importing and exporting .mid files needs the server MIDI routes, which are not in this build yet.";

    /// <inheritdoc />
    public string? Unavailable => Reason;

    /// <inheritdoc />
    public Task<EditResult> ImportAsync(TrackId track, double start, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EditResult.Refused(Reason));
    }

    /// <inheritdoc />
    public Task<EditResult> ExportAsync(ClipId clip, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EditResult.Refused(Reason));
    }
}
