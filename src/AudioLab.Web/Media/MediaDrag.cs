using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session;

namespace AudioLab.Web.Media;

/// <summary>A media file dragged from the library: where it would land on the lanes, and why it cannot when it cannot.</summary>
/// <remarks>
/// Nothing is sent while it moves. A press that never travels <see cref="ClipDrag.Threshold"/> pixels is a click, and a release away from the
/// audio lanes sends nothing. The start snaps to the grid the lanes draw, as a dragged clip does (D33).
/// </remarks>
public sealed class MediaDrag
{
    private const double LaneInset = 4;

    private readonly ProjectDocument _document;

    private readonly TimelineGeometry _geometry;

    private readonly TrackLayout _layout;

    private readonly TempoMap _tempo;

    private readonly double _startX;

    private readonly double _startY;

    /// <summary>Starts a drag of <paramref name="file"/>, pressed at a client point.</summary>
    public MediaDrag(MediaSummary file, ProjectDocument document, TimelineGeometry geometry, TrackLayout layout, double clientX, double clientY)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        File = file;
        _document = document;
        _geometry = geometry;
        _layout = layout;
        _tempo = TempoMap.From(document);
        _startX = clientX;
        _startY = clientY;
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

    /// <summary>Why the file cannot land where the pointer is, when the pointer is over a lane it does not fit; otherwise null.</summary>
    public string? Refusal { get; private set; }

    /// <summary>Moves the pointer to a client point.</summary>
    /// <param name="clientX">The pointer's client x.</param>
    /// <param name="clientY">The pointer's client y.</param>
    /// <param name="lanes">Where the top left of the lane surface is in client pixels when the pointer is over the lanes; null when it is not.</param>
    public void Move(double clientX, double clientY, (double Left, double Top)? lanes)
    {
        if (!Active && Math.Abs(clientX - _startX) < ClipDrag.Threshold && Math.Abs(clientY - _startY) < ClipDrag.Threshold)
        {
            return;
        }
        Active = true;
        PointerX = clientX;
        PointerY = clientY;
        Target = null;
        Refusal = null;
        if (lanes is not { } origin)
        {
            return;
        }
        double laneX = clientX - origin.Left;
        double laneY = clientY - origin.Top;
        if (laneX < 0 || laneY < 0 || _layout.Tops.Count == 0)
        {
            return;
        }
        int row = _layout.RowAt(laneY);
        Track track = _document.Tracks[row];
        if (track.Kind != TrackKind.Audio)
        {
            Refusal = $"\"{File.Name}\" is audio and goes on an audio track; \"{track.Name}\" is {TrackKindText.Phrase(track.Kind)}.";
            return;
        }
        double start = GridSnap.Seconds(_tempo, _document.TimeSignature, _document.View.Grid, _geometry.Seconds(laneX));
        Target = new MediaTarget(
            row,
            start,
            _geometry.X(start) + 1,
            _layout.Tops[row] + LaneInset,
            Math.Max(2, _geometry.Width(File.Duration) - 2),
            _layout.Heights[row] - (2 * LaneInset) - 1);
    }

    /// <summary>Releases the file: where to add it, or null when it is a click or the pointer is not over an audio lane.</summary>
    public MediaPlacement? Commit()
    {
        return Active && Target is { } target ? new MediaPlacement(File, _document.Tracks[target.Row].Id, target.Start) : null;
    }
}
