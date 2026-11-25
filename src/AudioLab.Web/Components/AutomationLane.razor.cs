using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue automation lane (D69): a line of one track's volume or pan, with a point where each point of the lane is.</summary>
/// <remarks>
/// The lane redraws only when its own points, parameter, selection, drag, mode or size changed, so a new revision costs the lanes that changed
/// and no others. Its keys and drags are handed up: the lane holds no state the document does not, but the selection and the drag are the screen's.
/// </remarks>
public sealed partial class AutomationLane
{
    // A point is drawn as a disc of --space-8, so its centre is half of that from its left and top edges.
    private const double PointRadius = 4;

    private (object?, object?, int?, int?, double, double, string, bool, AutomationParameter, string?, double) _shown;

    private bool _drawn;

    /// <summary>The lane's plan: where its times and values sit in its pixels.</summary>
    [Parameter, EditorRequired]
    public AutomationLanePlan Plan { get; set; } = null!;

    /// <summary>The points of the lane as the document holds them.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<AutomationPoint> Points { get; set; } = [];

    /// <summary>The points as the lane would leave them while a point is dragged; null when none is.</summary>
    [Parameter]
    public IReadOnlyList<AutomationPoint>? Preview { get; set; }

    /// <summary>The index of the selected point, or null.</summary>
    [Parameter]
    public int? Selected { get; set; }

    /// <summary>Where the dragged point sits in <see cref="Preview"/>, or null when none is dragged.</summary>
    [Parameter]
    public int? PreviewIndex { get; set; }

    /// <summary>The top of the lane in pixels from the top of the lanes.</summary>
    [Parameter]
    public double Top { get; set; }

    /// <summary>The width of the lane in pixels.</summary>
    [Parameter]
    public double Width { get; set; }

    /// <summary>The track colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>The track's name, for the lane's accessible name.</summary>
    [Parameter, EditorRequired]
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Whether the lane may be edited now.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Why it may not, for the lane's tooltip; null when it may.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised with a point's index and the press when the primary button goes down on it, which may begin a drag.</summary>
    [Parameter]
    public EventCallback<(int Index, PointerEventArgs Press)> OnPointDown { get; set; }

    /// <summary>Raised with the pixel across and down the lane when an empty part of it is double-clicked.</summary>
    [Parameter]
    public EventCallback<(double X, double Y)> OnAdd { get; set; }

    /// <summary>Raised for a key the lane handles.</summary>
    [Parameter]
    public EventCallback<KeyboardEventArgs> OnKey { get; set; }

    private IReadOnlyList<AutomationPoint> Shown => Preview ?? Points;

    private int? SelectedIndex => PreviewIndex ?? Selected;

    private string Name => $"Automation of {TrackName}, {(Plan.Parameter == AutomationParameter.Volume ? "volume" : "pan")}";

    private string Path => Plan.Path(Shown, Width);

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        (object?, object?, int?, int?, double, double, string, bool, AutomationParameter, string?, double) shown =
            (Points, Preview, Selected, PreviewIndex, Width, Plan.Height, ColorToken, Editable, Plan.Parameter, Reason, Top);
        bool same = _drawn && _shown.Equals(shown);
        _shown = shown;
        _drawn = true;
        return !same;
    }

    private Task PointDownAsync(int index, PointerEventArgs press)
    {
        return Editable ? OnPointDown.InvokeAsync((index, press)) : Task.CompletedTask;
    }

    private Task DoubleClickAsync(MouseEventArgs press)
    {
        return Editable ? OnAdd.InvokeAsync((press.OffsetX, press.OffsetY)) : Task.CompletedTask;
    }

    private Task KeyAsync(KeyboardEventArgs key) => OnKey.InvokeAsync(key);
}
