using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>A lane's steps as a row of toggles. A press starts a paint drag that sets the steps the pointer crosses to one absolute state; the drag's
/// work is in <see cref="StepStroke"/>, and only this row re-renders while the pointer moves. The keys go to the parent through callbacks.</summary>
public sealed partial class StepGrid
{
    private StepStroke? _stroke;

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

    // The state a step shows: the paint of a drag in progress, else the pattern's.
    private bool Shown(int step)
    {
        return _stroke is not null && _stroke.Painted.TryGetValue(step, out bool painted) ? painted : Lane.Steps[step];
    }

    private async Task PressAsync(int step)
    {
        await Press.InvokeAsync(step);
        if (!Editable || _stroke is not null)
        {
            return;
        }

        _stroke = StartStroke(!Shown(step));
        _stroke.Changed += Refresh;
        if (_stroke.Paint(step, Shown(step)))
        {
            StateHasChanged();
        }
    }

    // Only the pointer moves over the steps; the parent is not asked to render for each one.
    private void EnterAsync(int step)
    {
        if (_stroke is { Ended: false } stroke && stroke.Paint(step, Shown(step)))
        {
            StateHasChanged();
        }
    }

    private async Task ReleaseAsync()
    {
        if (_stroke is not { } stroke || stroke.Ended)
        {
            return;
        }

        await stroke.EndAsync();
        await EndStroke.InvokeAsync(stroke);
        stroke.Changed -= Refresh;
        _stroke = null;
        StateHasChanged();
    }

    private Task KeyAsync(int step, KeyboardEventArgs key) => Key.InvokeAsync(new StepKey(step, key));

    private Task FocusAsync(bool inside) => FocusChanged.InvokeAsync(inside);

    private void Refresh() => InvokeAsync(StateHasChanged);

    /// <summary>A key pressed on a step: which step, and the key.</summary>
    /// <param name="Step">The step, counted from 0.</param>
    /// <param name="Key">The key press.</param>
    public readonly record struct StepKey(int Step, KeyboardEventArgs Key);
}
