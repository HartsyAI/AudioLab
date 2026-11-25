using System.Globalization;
using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue knob: a disc with a pointer and its value; with <see cref="OnCommit"/> it is a slider you drag, step and reset.</summary>
public sealed partial class Knob : IDisposable
{
    private const double SweepDegrees = 135;

    private SliderInput? _slider;

    /// <summary>The setting from -1 (full left) through 0 (centre) to 1 (full right).</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>The value as written beside the knob.</summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>What the knob sets, as a screen reader says it.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Raised on release or after a key, with the new setting from -1 to 1; with it the knob is a slider that can be changed.</summary>
    [Parameter]
    public EventCallback<double> OnCommit { get; set; }

    /// <summary>Raised when the knob goes away while a drag on it was still going on, so that what the drag would have set is not sent.</summary>
    [Parameter]
    public EventCallback OnAbandon { get; set; }

    /// <summary>The setting a double-click or the reset key puts the knob back to, from -1 to 1.</summary>
    [Parameter]
    public double DefaultPosition { get; set; }

    /// <summary>Writes the value at a setting from -1 to 1 while it is being dragged; without it the text stays as given.</summary>
    [Parameter]
    public Func<double, string>? Format { get; set; }

    /// <summary>Whether the knob does nothing now; it stays focusable and its title says why.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Why the knob does nothing now, in one sentence.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>The key that resets the knob, as the shortcut registry writes it; its title says so.</summary>
    [Parameter]
    public string? ResetShortcut { get; set; }

    [Inject]
    private IPointerTracker Pointer { get; set; } = default!;

    private SliderInput Slider => _slider ??= new SliderInput(
        () => (Math.Clamp(Position, -1, 1) + 1) / 2,
        Pointer,
        () => StateHasChanged(),
        work => InvokeAsync(work),
        value => OnCommit.InvokeAsync((value * 2) - 1));

    private bool Interactive => OnCommit.HasDelegate;

    // The place on the travel from 0 to 1 that is drawn: the drag or the key being answered, else the setting.
    private double Travel => Slider.Travel;

    private string Transform => $"rotate({(((Travel * 2) - 1) * SweepDegrees).ToString("0.#", CultureInfo.InvariantCulture)} 12 12)";

    private string Now => Math.Round(Travel * 100).ToString(CultureInfo.InvariantCulture);

    private string Shown => Format is { } format && Slider.Previewing ? format((Travel * 2) - 1) : Text;

    private string Hint
    {
        get
        {
            if (Disabled)
            {
                return Reason ?? "Not available.";
            }
            return ResetShortcut is null ? $"{Label}: {Shown}" : $"{Label}: {Shown}. Double-click or {ResetShortcut} resets it.";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        bool dragging = _slider?.Gesture is { Active: true };
        _slider?.Dispose();
        if (dragging)
        {
            _ = OnAbandon.InvokeAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The new setting has been drawn, so the preview that stood in for it can go.
        Slider.Arrived((Math.Clamp(Position, -1, 1) + 1) / 2);
    }

    private Task PressAsync(PointerEventArgs press)
    {
        Slider.Press(press, Disabled);
        return Task.CompletedTask;
    }

    private Task KeyAsync(KeyboardEventArgs key)
    {
        return Slider.Key(key, Disabled, (Math.Clamp(DefaultPosition, -1, 1) + 1) / 2);
    }

    private Task ResetAsync()
    {
        return Slider.Reset((Math.Clamp(DefaultPosition, -1, 1) + 1) / 2, Disabled);
    }
}
