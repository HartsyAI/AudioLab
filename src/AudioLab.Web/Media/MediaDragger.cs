using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Session;

namespace AudioLab.Web.Media;

/// <summary>Runs a drag of a media file from the library onto the lanes: follows the pointer, shows where it lands, and adds it on release.</summary>
/// <remarks>
/// Like a clip drag (D33) it listens on the document, not on an element, and Escape cancels it. The file is added with one
/// <c>clip_add</c>, so one undo step undoes it, and a release away from an audio lane sends nothing; over a lane it does not fit it says why.
/// </remarks>
public sealed class MediaDragger(IPointerTracker pointer, IElementMetrics metrics, MediaDragState state, TimelineZoom zoom, ToastService toasts)
{
    private const string LaneSurfaceId = "lane-surface";

    /// <summary>Begins following the pointer for a press on a file at a client point.</summary>
    /// <param name="file">The file pressed.</param>
    /// <param name="document">The document as it is now, asked for at every move and at the release.</param>
    /// <param name="add">Adds the file as a clip where it was released.</param>
    /// <param name="clientX">Where the press was, client x.</param>
    /// <param name="clientY">Where the press was, client y.</param>
    /// <param name="blocked">Why nothing can be added now, or null.</param>
    public void Begin(
        MediaSummary file,
        Func<ProjectDocument> document,
        Func<MediaPlacement, Task> add,
        double clientX,
        double clientY,
        string? blocked)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(add);
        MediaDrag drag = new(file, clientX, clientY, blocked);
        state.Begin(drag);
        pointer.Begin(
            (x, y) =>
            {
                drag.Move(x, y, LanesAt(x, y), document(), zoom.Geometry);
                state.Moved();
            },
            (x, y) =>
            {
                drag.Move(x, y, LanesAt(x, y), document(), zoom.Geometry);
                _ = ReleaseAsync(drag, add);
            },
            () => state.End(drag));
    }

    /// <summary>Lets go of a drag in progress without adding anything, for a panel that goes while a file is lifted.</summary>
    public void Cancel()
    {
        if (state.Current is { } drag)
        {
            pointer.Stop();
            state.End(drag);
        }
    }

    private (double Left, double Top)? LanesAt(double x, double y) => metrics.IsOver(LaneSurfaceId, x, y) ? metrics.Origin(LaneSurfaceId) : null;

    private async Task ReleaseAsync(MediaDrag drag, Func<MediaPlacement, Task> add)
    {
        try
        {
            if (drag.Commit() is { } placement)
            {
                // The mark stays where the file lands until the server has answered and the clip is drawn.
                state.Moved();
                await add(placement);
            }
            else if (drag.Active && drag.Refusal is { } reason)
            {
                toasts.Show(ToastKind.Error, reason);
            }
        }
        finally
        {
            state.End(drag);
        }
    }
}
