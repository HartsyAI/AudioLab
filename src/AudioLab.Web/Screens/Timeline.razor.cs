using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The arrangement: one scroll container of rulers, track headers and lanes, with the corner and the headers sticky.</summary>
public sealed partial class Timeline : IDisposable
{
    private const string ElementId = "timeline";

    private ProjectDocument? _laidOut;

    private TrackLayout _layout = null!;

    private IReadOnlyList<BarLine> _bars = [];

    private double _length;

    private ClipId? _tabStop;

    private bool _guarded;

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project; when it cannot edit, the controls that would say why they do nothing.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private TrackColorMap Colors { get; set; } = default!;

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    [Inject]
    private IKeyGuard Guard { get; set; } = default!;

    [Inject]
    private IPointerTracker Pointer { get; set; } = default!;

    [Inject]
    private IElementMetrics Metrics { get; set; } = default!;

    [Inject]
    private ClipDragState Drags { get; set; } = default!;

    private TimelineGeometry Geometry => TimelineGeometry.Default;

    private TrackLayout Layout => _layout;

    private IReadOnlyList<BarLine> Bars => _bars;

    private double Length => _length;

    private double LaneWidth => Math.Round(Geometry.Width(_length));

    private ClipId? TabStop => Selection.Clip ?? _tabStop;

    private IReadOnlyList<RulerRowKind> Kinds
    {
        get
        {
            List<RulerRowKind> kinds = [RulerRowKind.Sections];
            if (Document.Markers.Count > 0)
            {
                kinds.Add(RulerRowKind.Markers);
            }
            kinds.Add(RulerRowKind.Bars);
            if (Document.View.Ruler == RulerMode.Time)
            {
                kinds.Add(RulerRowKind.Time);
            }
            return kinds;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Selection.Changed -= OnSelectionChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Selection.Changed += OnSelectionChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_laidOut, Document))
        {
            return;
        }
        _laidOut = Document;
        _layout = TrackLayout.Of(Document);
        _length = SessionExtent.LengthSeconds(Document);
        _bars = BarGrid.Lines(Document, _length);
        Selection.Prune(Document);
        if (_tabStop is not { } stop || TrackOf(stop) is null)
        {
            _tabStop = ClipNavigator.First(Document);
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (!_guarded && OperatingSystem.IsBrowser())
        {
            _guarded = true;
            Guard.PreventNavigationKeys(ElementId, "button");
        }
    }

    private void OnSelectionChanged() => _ = InvokeAsync(StateHasChanged);

    private Task AddTrack() => Actions.AddTrack();

    private void SelectTrack(TrackId track) => Selection.SelectTrack(track);

    private void SelectClip(ClipId clip)
    {
        TrackId? track = TrackOf(clip);
        if (track is { } owner)
        {
            _tabStop = clip;
            Selection.SelectClip(owner, clip);
        }
    }

    private async Task ClipKeyAsync((ClipId Clip, KeyboardEventArgs Key) press)
    {
        if (press.Key.AltKey)
        {
            if (Actions.CanEdit && ClipMoves.Nudge(Document, press.Clip, press.Key.Key) is { } nudge)
            {
                await Actions.MoveClip(nudge);
            }
            return;
        }
        ClipId? target = ClipNavigator.Next(Document, press.Clip, press.Key.Key);
        if (target is { } next)
        {
            SelectClip(next);
            await Task.Yield();
            Focus.Focus(Components.Clip.IdOf(next));
        }
    }

    // A press on a clip selects it and, when the project can be edited, may become a drag; nothing is sent until the release.
    private Task ClipPointerDownAsync((ClipId Clip, PointerEventArgs Press) down)
    {
        SelectClip(down.Clip);
        (double Left, double Top)? origin = Metrics.Origin("lane-surface");
        if (!Actions.CanEdit || origin is not { } at)
        {
            return Task.CompletedTask;
        }
        double left = at.Left;
        double top = at.Top;
        ClipDrag drag = new(Document, down.Clip, Geometry, Layout, down.Press.ClientX - left, down.Press.ClientY - top);
        Drags.Begin(drag);
        Pointer.Begin(
            (x, y) =>
            {
                drag.Move(x - left, y - top);
                Drags.Moved();
            },
            (x, y) =>
            {
                drag.Move(x - left, y - top);
                _ = InvokeAsync(() => ReleaseAsync(drag));
            },
            () => Drags.End());
        return Task.CompletedTask;
    }

    private async Task ReleaseAsync(ClipDrag drag)
    {
        ClipMove? move = drag.Commit();
        Drags.End();
        if (move is { } send)
        {
            await Actions.MoveClip(send);
        }
    }

    private TrackId? TrackOf(ClipId clip)
    {
        return Document.Tracks.FirstOrDefault(track => track.Clips.Any(c => c.Id == clip) || track.MidiClips.Any(c => c.Id == clip))?.Id;
    }
}
