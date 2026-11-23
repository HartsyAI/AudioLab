using AudioLab.Core.Api;
using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The transfer the page has until the typed client of the server's MIDI routes is written: it says why, and sends nothing.</summary>
public sealed class UnavailableMidiTransfer : IMidiTransfer
{
    private const string Reason = "Importing and exporting .mid files waits for the page's client of the server's MIDI routes, not written yet.";

    /// <inheritdoc />
    public string? Unavailable => Reason;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Always: nothing is sent, and a success is never made up.</exception>
    public Task<MidiImportResult> ImportAsync(
        ProjectId project,
        TrackId track,
        string name,
        double start,
        Stream file,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(Reason);
    }

    /// <inheritdoc />
    public Uri? ExportAddress(ProjectId project, ClipId clip) => null;
}
