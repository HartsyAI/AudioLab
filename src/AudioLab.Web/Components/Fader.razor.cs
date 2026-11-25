using System.Globalization;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue channel fader: a slot, a cap and the decibel scale at its left; with <see cref="OnCommit"/> it is a slider.</summary>
/// <remarks>It is dragged, stepped and reset as a knob is, and a disabled one stays in the tab order and says why.</remarks>
public sealed partial class Fader : IDisposable
{
    private SliderInput? _slider;

    /// <summary>The position along the scale, from 0 at the bottom to 1 at the top.</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>The gain written as decibels, for a screen reader.</summary>
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>What the fader sets, as a screen reader says it.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Raised on release or after a key, with the new position from 0 to 1; with it the fader is a slider that can be changed.</summary>
    [Parameter]
    public EventCallback<double> OnCommit { get; set; }

    /// <summary>The position a double-click or the reset key puts the fader back to, from 0 to 1.</summary>
    [Parameter]
    public double DefaultPosition { get; set; }

    /// <summary>Writes the value at a position from 0 to 1 while it is dragged; without it the text stays as given.</summary>
    [Parameter]
    public Func<double, string>? Format { get; set; }

    /// <summary>Whether the fader does nothing now; it stays focusable and its title says why.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Why the fader does nothing now, in one sentence.</summary>
    [Parameter]
    public string? Reason { get; set; }

    [Inject]
    private IPointerTracker Pointer { get; set; } = default!;

    private SliderInput Slider => _slider ??= new SliderInput(
        () => Math.Clamp(Position, 0, 1),
        Pointer,
        () => StateHasChanged(),
        work => InvokeAsync(work),
        value => OnCommit.InvokeAsync(value));

    private bool Interactive => OnCommit.HasDelegate;

    private double Travel => Slider.Travel;

    private string Percent => (Travel * 100).ToString("0.##", CultureInfo.InvariantCulture);

    private string Now => Math.Round(Travel * 100).ToString(CultureInfo.InvariantCulture);

    private string Shown => Format is { } format && Slider.Previewing ? format(Travel) : Text;

    // The value in words for a screen reader: a readout without its unit gets the decibels.
    private string ValueText
    {
        get
        {
            string shown = Shown;
            return shown == "-inf" || shown.EndsWith(" dB", StringComparison.Ordinal) ? shown : $"{shown} dB";
        }
    }

    private string Hint
    {
        get
        {
            if (Disabled)
            {
                return Reason ?? "Not available.";
            }
            return $"{Label}: {ValueText}. Double-click or {Shortcuts.ControlReset.Display} resets it.";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _slider?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The new position has been drawn, so the preview that stood in for it can go.
        Slider.Arrived(Math.Clamp(Position, 0, 1));
    }

    private Task PressAsync(PointerEventArgs press)
    {
        Slider.Press(press, Disabled || !Interactive);
        return Task.CompletedTask;
    }

    private Task KeyAsync(KeyboardEventArgs key)
    {
        return Slider.Key(key, Disabled, Math.Clamp(DefaultPosition, 0, 1));
    }

    private Task ResetAsync()
    {
        return Slider.Reset(Math.Clamp(DefaultPosition, 0, 1), Disabled);
    }
}
