using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Media;

/// <summary>A media file dragged from the library: where it would land on the lanes, and why it cannot when it cannot.</summary>
/// <remarks>
/// Nothing is sent while it moves. A press that never travels <see cref="ClipDrag.Threshold"/> pixels is a click, and a release away from the
/// audio lanes sends nothing. The start snaps to the grid the lanes draw, as a dragged clip does (D33). The document and the zoom are given at
/// every move, so a track that another client adds or removes during the drag is the track the release lands on, not the one at the press.
/// </remarks>
public sealed class MediaDrag
{
    private const double LaneInset = 4;

    private readonly double _startX;

    private readonly double _startY;

    private readonly string? _blocked;

    private TrackId? _track;

    private double _start;

    /// <summary>Starts a drag of <paramref name="file"/>, pressed at a client point.</summary>
    /// <param name="file">The file pressed.</param>
    /// <param name="clientX">Where the press was, client x.</param>
    /// <param name="clientY">Where the press was, client y.</param>
    /// <param name="blocked">Why nothing can be added now, when the project cannot be edited; the drag then follows the pointer and says so.</param>
    public MediaDrag(MediaSummary file, double clientX, double clientY, string? blocked = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        File = file;
        _startX = clientX;
        _startY = clientY;
        _blocked = blocked;
        PointerX = clientX;
        PointerY = clientY;
    }

    /// <summary>The file being dragged.</summary>
    public MediaSummary File { get; }

    /// <summary>Whether the pointer has travelled far enough for this to be a drag and not a click.</summary>
    public bool Active { get; private set; }

    /// <summary>The pointer's client x, where the lifted label follows it.</summary>
    public double PointerX { get; private set; }

    /// <summary>The pointer's client y.</summary>
    public double PointerY { get; private set; }

    /// <summary>Where the file would land if it were released now, or null when the pointer is not over a lane it fits.</summary>
    public MediaTarget? Target { get; private set; }

    /// <summary>Why the file cannot land where the pointer is; null when it can, or when the pointer is not over a lane.</summary>
    public string? Refusal { get; private set; }

    /// <summary>Moves the pointer to a client point.</summary>
    /// <param name="clientX">The pointer's client x.</param>
    /// <param name="clientY">The pointer's client y.</param>
    /// <param name="lanes">Where the top left of the lane surface is in client pixels when the pointer is over the lanes, else null.</param>
    /// <param name="document">The document as it is now.</param>
    /// <param name="geometry">The scale of the lanes as it is now.</param>
    public void Move(double clientX, double clientY, (double Left, double Top)? lanes, ProjectDocument document, TimelineGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Active && Math.Abs(clientX - _startX) < ClipDrag.Threshold && Math.Abs(clientY - _startY) < ClipDrag.Threshold)
        {
            return;
        }
        Active = true;
        PointerX = clientX;
        PointerY = clientY;
        Target = null;
        Refusal = _blocked is null ? null : $"{File.Name} cannot be added now. {_blocked}";
        _track = null;
        if (_blocked is not null || lanes is not { } origin)
        {
            return;
        }
        TrackLayout layout = TrackLayout.Of(document);
        double laneX = clientX - origin.Left;
        double laneY = clientY - origin.Top;
        // Below the last lane is not a lane: the last track is not where the file lands.
        if (laneX < 0 || laneY < 0 || laneY >= layout.Total)
        {
            return;
        }
        int row = layout.RowAt(laneY);
        Track track = document.Tracks[row];
        if (track.Kind != TrackKind.Audio)
        {
            Refusal = $"\"{File.Name}\" is audio and goes on an audio track; \"{track.Name}\" is {TrackKindText.Phrase(track.Kind)}.";
            return;
        }
        _start = new GridSnap(document).Snap(geometry.Seconds(laneX));
        _track = track.Id;
        Target = new MediaTarget(
            row,
            _start,
            geometry.X(_start) + 1,
            layout.Tops[row] + LaneInset,
            Math.Max(2, geometry.Width(File.Duration) - 2),
            layout.Heights[row] - (2 * LaneInset) - 1);
    }

    /// <summary>Releases the file: where to add it, or null when it is a click or the pointer is not over an audio lane.</summary>
    public MediaPlacement? Commit()
    {
        return Active && _track is { } track ? new MediaPlacement(File, track, _start) : null;
    }
}
