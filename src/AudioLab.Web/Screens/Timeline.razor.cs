using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The arrangement: one scroll container of rulers, track headers and lanes, with the corner and the headers sticky.</summary>
public sealed partial class Timeline : IDisposable
{
    private const string ElementId = "timeline";

    private const string LaneSurfaceId = "lane-surface";

    private ProjectDocument? _laidOut;

    private TrackLayout _layout = null!;

    private IReadOnlyList<BarLine> _bars = [];

    private double _length;

    private ClipId? _tabStop;

    private bool _guarded;

    private ClipDrag? _drag;

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project; when it cannot edit, the controls that would say why they do nothing.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    [Inject]
    private TimelineZoom Zoom { get; set; } = default!;

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

    [Inject]
    private BottomPanelState Panel { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    [Inject]
    private PlaybackService Playback { get; set; } = default!;

    private TimelineGeometry Geometry => Zoom.Geometry;

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
        Lanes.Changed -= OnLanesChanged;
        if (_drag is { } drag)
        {
            // The screen went while a clip was lifted: the listeners are let go and the drag is dropped without a move.
            _drag = null;
            Pointer.Stop();
            Drags.End(drag);
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Selection.Changed += OnSelectionChanged;
        Lanes.Changed += OnLanesChanged;
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
        PruneAutomation();
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

    private Task AddInstrument() => Actions.AddInstrumentTrack();
    // The lanes start where the ruler does, so a press in the bars row is a time in the lanes' own pixels once their left edge is taken off.
    private void RulerSeek(MouseEventArgs press)
    {
        if (Metrics.Origin(LaneSurfaceId) is { } origin)
        {
            Playback.Seek(Geometry.Seconds(Math.Max(0, press.ClientX - origin.Left)));
        }
    }

    private void SelectTrack(TrackId track) => Selection.SelectTrack(track);

    // A MIDI clip is opened in the piano roll: it becomes the selection and the panel shows its editor.
    private void OpenClip(ClipId clip)
    {
        SelectClip(clip);
        Panel.Show(BottomPanelState.PianoRoll);
    }

    // A double-click on an empty part of an instrument lane makes a clip of one bar there, on the line the grid draws nearest to it.
    private async Task LaneDoubleClickAsync((TrackId Track, double X) click)
    {
        Track? track = Document.Tracks.FirstOrDefault(candidate => candidate.Id == click.Track);
        if (track is not { Kind: TrackKind.Instrument } || !Actions.CanEdit)
        {
            return;
        }
        double start = new GridSnap(Document).Snap(Geometry.Seconds(click.X));
        EditResult result = await Actions.Midi.AddClip(track.Id, start);
        if (result.Succeeded && result.Created.Count > 0)
        {
            Selection.SelectClip(track.Id, ClipId.From(result.Created[0]));
        }
    }

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
        if (IsNudge(press.Key))
        {
            if (Actions.CanEdit && ClipMoves.Nudge(Document, press.Clip, press.Key.Key) is { } nudge)
            {
                await Actions.MoveClip(nudge);
            }
            return;
        }
        if (press.Key.AltKey || press.Key.CtrlKey || press.Key.MetaKey)
        {
            // A chord that is not the arrangement's keeps its own meaning; Alt or Ctrl with an arrow is not a step to another clip.
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

    // The nudge keys are the registry's: Alt with an arrow key, and no other modifier.
    private static bool IsNudge(KeyboardEventArgs key)
    {
        return Shortcuts.NudgeLeft.Matches(key) || Shortcuts.NudgeRight.Matches(key)
            || Shortcuts.NudgeUp.Matches(key) || Shortcuts.NudgeDown.Matches(key);
    }

    // A press on a clip selects it and, when the project can be edited, may become a drag; nothing is sent until the release.
    private Task ClipPointerDownAsync((ClipId Clip, PointerEventArgs Press) down)
    {
        SelectClip(down.Clip);
        (double Left, double Top)? origin = Metrics.Origin(LaneSurfaceId);
        if (!Actions.CanEdit || origin is not { } at)
        {
            return Task.CompletedTask;
        }
        ClipDrag drag = new(Document, down.Clip, Geometry, Layout, down.Press.ClientX - at.Left, down.Press.ClientY - at.Top);
        _drag = drag;
        Drags.Begin(drag);
        // The lanes scroll under the pointer, so where the pointer is in them is read again on every move and on the release.
        Pointer.Begin(
            (x, y) =>
            {
                if (Metrics.Origin(LaneSurfaceId) is { } now)
                {
                    drag.Move(x - now.Left, y - now.Top);
                    Drags.Moved();
                }
            },
            (x, y) =>
            {
                if (Metrics.Origin(LaneSurfaceId) is { } now)
                {
                    drag.Move(x - now.Left, y - now.Top);
                }
                _ = InvokeAsync(() => ReleaseAsync(drag));
            },
            () => Finish(drag));
        return Task.CompletedTask;
    }

    private void Finish(ClipDrag drag)
    {
        _drag = null;
        Drags.End(drag);
    }

    private async Task ReleaseAsync(ClipDrag drag)
    {
        ClipMove? move = drag.Commit();
        if (move is not { } send)
        {
            Finish(drag);
            return;
        }
        if (!Actions.CanEdit)
        {
            Toasts.Show(ToastKind.Warning, "The connection to the server was lost during the drag, so the clip was not moved.");
            Finish(drag);
            return;
        }
        // The clip stays where it lands, with its landing mark, until the move is answered and the new revision is drawn.
        drag.Settle();
        Drags.Moved();
        try
        {
            await Actions.MoveClip(send);
        }
        finally
        {
            Finish(drag);
        }
    }

    private TrackId? TrackOf(ClipId clip)
    {
        return Document.Tracks.FirstOrDefault(track => track.Clips.Any(c => c.Id == clip) || track.MidiClips.Any(c => c.Id == clip))?.Id;
    }
}
