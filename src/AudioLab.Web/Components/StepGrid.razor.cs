using AudioLab.Core.Model;
using AudioLab.Web.Interop;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>A lane's steps as a row of toggles. A press starts a paint drag that sets the steps the pointer crosses to one absolute state.
/// The drag's work is in <see cref="StepStroke"/>; only this row re-renders while the pointer moves. The keys go to the parent through callbacks.</summary>
public sealed partial class StepGrid : IDisposable
{
    // The drag the pointer is in. A drag that has been released stays in _settling until the server has answered it, so a new press can start
    // a drag while the last one is still on its way.
    private StepStroke? _stroke;

    private readonly List<StepStroke> _settling = [];

    private bool _dirty = true;

    private bool _disposed;

    /// <summary>The lane whose steps these are.</summary>
    [Parameter, EditorRequired]
    public BeatLane Lane { get; set; } = null!;

    /// <summary>Whether a press may change a step; when false the steps say why in their titles and a press only reports it.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Why the steps cannot change, for their titles.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>The step that takes the tab stop of the grid, or -1 for none.</summary>
    [Parameter]
    public int Cursor { get; set; }

    /// <summary>Starts a paint drag that makes the steps it touches on, or off when false.</summary>
    [Parameter, EditorRequired]
    public Func<bool, StepStroke> StartStroke { get; set; } = null!;

    /// <summary>Hears that a paint drag has ended, once its commands have been answered.</summary>
    [Parameter, EditorRequired]
    public EventCallback<StepStroke> EndStroke { get; set; }

    /// <summary>Hears a press on a step, whether or not the steps can change.</summary>
    [Parameter, EditorRequired]
    public EventCallback<int> Press { get; set; }

    /// <summary>Hears a key pressed on a step.</summary>
    [Parameter, EditorRequired]
    public EventCallback<StepKey> Key { get; set; }

    /// <summary>Hears that the focus went into or out of the grid.</summary>
    [Parameter, EditorRequired]
    public EventCallback<bool> FocusChanged { get; set; }

    [Inject]
    private IElementMetrics Metrics { get; set; } = default!;

    // The state a step shows: the paint of the drag in progress, then of the drags waiting for the server (the newest first), else the pattern's.
    private bool Shown(int step)
    {
        if (_stroke is not null && _stroke.Painted.TryGetValue(step, out bool painted))
        {
            return painted;
        }

        for (int index = _settling.Count - 1; index >= 0; index--)
        {
            if (_settling[index].Painted.TryGetValue(step, out bool settled))
            {
                return settled;
            }
        }

        return Lane.Steps[step];
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => _dirty = true;

    /// <inheritdoc />
    protected override bool ShouldRender() => _dirty;

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender) => _dirty = false;

    internal async Task PressAsync(int step)
    {
        await Press.InvokeAsync(step);
        if (!Editable || _stroke is not null || _disposed)
        {
            return;
        }

        bool on = !Shown(step);
        StepStroke stroke = StartStroke(on);
        _stroke = stroke;
        stroke.Changed += Refresh;
        if (stroke.Paint(step, Shown(step)))
        {
            Repaint();
        }
    }

    // Only the pointer moves over the steps; the parent is not asked to render for each one, and a move that paints nothing renders nothing.
    internal void Enter(int step)
    {
        if (_stroke is { Ended: false } stroke && stroke.Paint(step, Shown(step)))
        {
            Repaint();
        }
    }

    // A touch keeps the press on the cell it began on, so the pointer's enter events do not come; the cell under the finger is found instead.
    internal void TouchMove(PointerEventArgs pointer)
    {
        if (pointer.PointerType != "touch" || _stroke is not { Ended: false })
        {
            return;
        }

        for (int step = 0; step < Lane.Steps.Count; step++)
        {
            if (Metrics.IsOver(BeatLaneText.CellId(Lane.Id, step), pointer.ClientX, pointer.ClientY))
            {
                Enter(step);
                return;
            }
        }
    }

    // The drag is released: no step is painted after this, and the next press may start a drag while this one is answered.
    internal Task ReleaseAsync()
    {
        if (_stroke is not { } stroke)
        {
            return Task.CompletedTask;
        }

        _stroke = null;
        _settling.Add(stroke);
        return SettleAsync(stroke);
    }

    // The drag's commands are answered, then its gesture is closed by the parent. A failure leaves the drag out of the way all the same.
    private async Task SettleAsync(StepStroke stroke)
    {
        try
        {
            await stroke.EndAsync();
            await EndStroke.InvokeAsync(stroke);
        }
        finally
        {
            stroke.Changed -= Refresh;
            _settling.Remove(stroke);
            if (!_disposed)
            {
                Repaint();
            }
        }
    }

    private Task KeyAsync(int step, KeyboardEventArgs key) => Key.InvokeAsync(new StepKey(step, key));

    private Task FocusAsync(bool inside) => FocusChanged.InvokeAsync(inside);

    private void Repaint()
    {
        _dirty = true;
        StateHasChanged();
    }

    private void Refresh()
    {
        if (!_disposed)
        {
            _ = InvokeAsync(Repaint);
        }
    }

    /// <summary>Ends the drag the grid holds when the grid goes, so that its gesture is closed and a later press is not blocked.</summary>
    public void Dispose()
    {
        _disposed = true;
        if (_stroke is not { } stroke)
        {
            return;
        }

        _stroke = null;
        _settling.Add(stroke);
        // The page is leaving, so there is nothing left to show a failure to: the gesture is closed all the same.
        _ = SettleAsync(stroke).ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>A key pressed on a step: which step, and the key.</summary>
    /// <param name="Step">The step, counted from 0.</param>
    /// <param name="Key">The key press.</param>
    public readonly record struct StepKey(int Step, KeyboardEventArgs Key);
}
