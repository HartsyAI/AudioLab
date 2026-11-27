using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue automation lane (D70): one track's volume or pan drawn as one SVG, with no element per point.</summary>
/// <remarks>
/// The lane redraws only when its own points, parameter, selection, drag, size or name changed, and it hears only the changes of its own
/// track, so a point moved by a drag costs this lane and no other. A lane that is drawn for the first time remembers what it drew (D68).
/// </remarks>
public sealed partial class AutomationLane : IDisposable
{
    private IReadOnlyList<AutomationPoint>? _points;

    private readonly AutomationLaneGate _gate = new();

    private string _curve = string.Empty;

    private string _dots = string.Empty;

    private string _selected = string.Empty;

    private string _overlay = string.Empty;

    private double _width;

    private double _scale;

    private double _height;

    /// <summary>The track whose lane this is.</summary>
    [Parameter, EditorRequired]
    public Track Track { get; set; } = null!;

    /// <summary>The screen's automation view state: the selection and the drag.</summary>
    [Parameter, EditorRequired]
    public AutomationLanes Lanes { get; set; } = null!;

    /// <summary>The lane's plan: where its times and values sit in its pixels.</summary>
    [Parameter, EditorRequired]
    public AutomationLanePlan Plan { get; set; } = null!;

    /// <summary>The top of the lane in pixels from the top of the lanes.</summary>
    [Parameter]
    public double Top { get; set; }

    /// <summary>The width of the lane in pixels.</summary>
    [Parameter]
    public double Width { get; set; }

    /// <summary>The track colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>Whether the lane may be edited now.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Why it may not, for the lane's tooltip; null when it may.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised with the press when the primary button goes down on the lane, which may select a point or begin a drag.</summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnPointerDown { get; set; }

    /// <summary>Raised with the double-click when the lane is double-clicked, which may add a point.</summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnDoubleClick { get; set; }

    /// <summary>Raised for a key the lane handles.</summary>
    [Parameter]
    public EventCallback<KeyboardEventArgs> OnKey { get; set; }

    private string ElementId => $"automation-{Track.Id.Value}";

    private string Name => $"Automation of {Track.Name}, {(Plan.Parameter == AutomationParameter.Volume ? "volume" : "pan")}";

    private IReadOnlyList<AutomationPoint> Points => AutomationRules.Lane(Track.Automation, Plan.Parameter);

    private string Curve => _curve;

    private string Dots => _dots;

    private string Selected => _selected;

    private string Overlay => _overlay;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Lanes.Changed += OnLanesChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_points, Points) || _width != Width || _scale != Plan.X(1) || _height != Plan.Height)
        {
            _points = Points;
            _width = Width;
            _scale = Plan.X(1);
            _height = Plan.Height;
            _curve = Plan.Path(_points, Width);
            _dots = Plan.Marks(_points, Width);
        }
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // The first render does not ask ShouldRender, so what it drew is remembered here; without it the next change would draw again for nothing.
        if (firstRender)
        {
            _gate.Remember(Snapshot());
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _gate.Draw(Snapshot());

    /// <inheritdoc />
    public void Dispose()
    {
        Lanes.Changed -= OnLanesChanged;
    }

    private AutomationLaneShown Snapshot()
    {
        AutomationDrag? drag = Lanes.Drag is { } active && active.Track == Track.Id ? active : null;
        return new AutomationLaneShown(
            _points,
            Lanes.Track == Track.Id ? Lanes.Index : null,
            drag is not null,
            drag?.Index,
            drag?.Time ?? 0,
            drag?.Value ?? 0,
            Width,
            Plan.Height,
            Top,
            ColorToken,
            Editable,
            Reason,
            Name,
            Plan.Parameter);
    }

    private void OnLanesChanged(TrackId track)
    {
        if (track == Track.Id)
        {
            Refresh();
            _ = InvokeAsync(StateHasChanged);
        }
    }

    // The marks follow the selection and the drag, so they are computed again whenever either changes, not only on a new parameter set.
    private void Refresh()
    {
        _selected = AutomationMarks.Selected(Plan, Points, Lanes.Track == Track.Id ? Lanes.Index : null);
        _overlay = AutomationMarks.Overlay(Plan, Points, Lanes.Drag is { } drag && drag.Track == Track.Id ? drag : null);
    }

    // Only the primary button presses a lane: a right-click starts nothing, as a fader's press does (SliderInput).
    private Task PointerDownAsync(PointerEventArgs press) =>
        Editable && press.Button == 0 ? OnPointerDown.InvokeAsync(press) : Task.CompletedTask;

    private Task DoubleClickAsync(MouseEventArgs press) => Editable ? OnDoubleClick.InvokeAsync(press) : Task.CompletedTask;

    private Task KeyAsync(KeyboardEventArgs key) => OnKey.InvokeAsync(key);
}
