using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue automation lane (D69): one track's volume or pan drawn as one SVG, with no element per point.</summary>
/// <remarks>
/// The lane redraws only when its own points, parameter, selection, drag, size or name changed, and it hears only the changes of its own
/// track, so a point moved by a drag costs this lane and no other. A lane that is drawn for the first time remembers what it drew (D68).
/// </remarks>
public sealed partial class AutomationLane : IDisposable
{
    private IReadOnlyList<AutomationPoint>? _points;

    private (double, double, bool, int?, int?, double, double, string, bool, string?, string, AutomationParameter, double, string) _shown;

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
        _selected = SelectedPath();
        _overlay = OverlayPath();
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // The first render does not ask ShouldRender, so what it drew is remembered here; without it the next change would draw again for nothing.
        if (firstRender)
        {
            _shown = Shown();
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        bool same = _shown.Equals(Shown());
        _shown = Shown();
        return !same;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Lanes.Changed -= OnLanesChanged;
    }

    private (double, double, bool, int?, int?, double, double, string, bool, string?, string, AutomationParameter, double, string) Shown()
    {
        AutomationDrag? drag = Lanes.Drag is { } active && active.Track == Track.Id ? active : null;
        return (Top, Width, drag is not null, Lanes.Track == Track.Id ? Lanes.Index : null, drag?.Index, drag?.Time ?? 0, drag?.Value ?? 0,
            ColorToken, Editable, Reason, Track.Name, Plan.Parameter, Plan.Height, ElementId);
    }

    private void OnLanesChanged(TrackId track)
    {
        if (track == Track.Id)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    // The selected point, drawn over the curve as a mark of its own.
    private string SelectedPath()
    {
        if (Lanes.Track != Track.Id || Lanes.Index is not { } index || index >= Points.Count)
        {
            return string.Empty;
        }
        AutomationPoint point = Points[index];
        return $"M{Format(Plan.X(point.Time))} {Format(Plan.Y(point.Value))}h0";
    }

    // While a point is dragged, only its own segments are drawn again: from its neighbours to where it is now.
    private string OverlayPath()
    {
        if (Lanes.Drag is not { } drag || drag.Track != Track.Id)
        {
            return string.Empty;
        }
        IReadOnlyList<AutomationPoint> points = Points;
        double x = Plan.X(drag.Time);
        double y = Plan.Y(drag.Value);
        string path = $"M{Format(x)} {Format(y)}";
        if (drag.Index > 0 && drag.Index - 1 < points.Count)
        {
            AutomationPoint before = points[drag.Index - 1];
            path = $"M{Format(Plan.X(before.Time))} {Format(Plan.Y(before.Value))}L{Format(x)} {Format(y)}";
        }
        if (drag.Index + 1 < points.Count)
        {
            AutomationPoint after = points[drag.Index + 1];
            path += $"L{Format(Plan.X(after.Time))} {Format(Plan.Y(after.Value))}";
        }
        return path;
    }

    private static string Format(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private Task PointerDownAsync(PointerEventArgs press) => Editable ? OnPointerDown.InvokeAsync(press) : Task.CompletedTask;

    private Task DoubleClickAsync(MouseEventArgs press) => Editable ? OnDoubleClick.InvokeAsync(press) : Task.CompletedTask;

    private Task KeyAsync(KeyboardEventArgs key) => OnKey.InvokeAsync(key);
}
