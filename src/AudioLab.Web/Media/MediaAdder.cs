using AudioLab.Core.Model;
using AudioLab.Web.Components;

namespace AudioLab.Web.Media;

/// <summary>Adds a media file to the arrangement for the keyboard and the add button: one add at a time, with the reason when it cannot.</summary>
public sealed class MediaAdder(ToastService toasts)
{
    private bool _adding;

    /// <summary>Adds the file on the selected audio track at the playhead or after the selected clip, unless one add is already on its way.</summary>
    /// <param name="file">The file to add.</param>
    /// <param name="document">The document the placement is worked out in.</param>
    /// <param name="track">The selected track, or null.</param>
    /// <param name="clip">The selected clip, or null.</param>
    /// <param name="playhead">The playhead time in seconds.</param>
    /// <param name="canEdit">Whether the project can be edited now.</param>
    /// <param name="reason">Why it cannot, when it cannot.</param>
    /// <param name="add">Adds the file as a clip where it is told to.</param>
    public async Task AddAsync(
        MediaSummary file,
        ProjectDocument document,
        TrackId? track,
        ClipId? clip,
        double playhead,
        bool canEdit,
        string? reason,
        Func<MediaPlacement, Task> add)
    {
        ArgumentNullException.ThrowIfNull(add);
        if (_adding)
        {
            return;
        }
        if (!canEdit)
        {
            toasts.Show(ToastKind.Warning, $"{file.Name} was not added. {reason}");
            return;
        }
        (MediaPlacement? placement, string? refusal) = ClipInsertion.For(document, file, track, clip, playhead);
        if (placement is not { } where)
        {
            toasts.Show(ToastKind.Error, refusal ?? $"{file.Name} could not be added.");
            return;
        }
        _adding = true;
        try
        {
            await add(where);
        }
        finally
        {
            _adding = false;
        }
    }
}
