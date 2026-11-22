using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The arrangement: one scroll container holding the rulers, the track headers and the lanes, with the corner and the headers sticky.</summary>
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

    /// <summary>Whether the arrangement may be edited; when not, the controls that would edit it say why they do nothing.</summary>
    [Parameter]
    public bool CanEdit { get; set; }

    /// <summary>Why the arrangement cannot be edited now.</summary>
    [Parameter]
    public string? ReadOnlyReason { get; set; }

    /// <summary>Raised when the user asks for a new track.</summary>
    [Parameter]
    public EventCallback OnAddTrack { get; set; }

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private TrackColorMap Colors { get; set; } = default!;

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    [Inject]
    private IKeyGuard Guard { get; set; } = default!;

    private TimelineGeometry Geometry => TimelineGeometry.Default;

    private TrackLayout Layout => _layout;

    private IReadOnlyList<BarLine> Bars => _bars;

    private double Length => _length;

    private double LaneWidth => Math.Round(Geometry.Width(_length));

    private ClipId? TabStop => Selection.Clip ?? _tabStop;

    private string? AddTrackReason => CanEdit ? null : ReadOnlyReason;

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
        if (_tabStop is not { } stop || !Document.Tracks.Any(track => track.Clips.Any(clip => clip.Id == stop) || track.MidiClips.Any(clip => clip.Id == stop)))
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

    private Task AddTrack() => OnAddTrack.InvokeAsync();

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
        ClipId? target = ClipNavigator.Next(Document, press.Clip, press.Key.Key);
        if (target is { } next)
        {
            SelectClip(next);
            await Task.Yield();
            Focus.Focus(Components.Clip.IdOf(next));
        }
    }

    private TrackId? TrackOf(ClipId clip)
    {
        return Document.Tracks.FirstOrDefault(track => track.Clips.Any(c => c.Id == clip) || track.MidiClips.Any(c => c.Id == clip))?.Id;
    }
}
