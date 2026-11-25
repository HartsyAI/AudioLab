using System.Globalization;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue channel fader: a slot, a cap and the decibel scale at its left; with <see cref="OnCommit"/> it is a slider.</summary>
/// <remarks>It is dragged, stepped and reset as a knob is, and a disabled one stays in the tab order and says why.</remarks>
public sealed partial class Fader : IDisposable
{
    private readonly ValueSender _sender;

    private ValueGesture? _gesture;

    /// <summary>Creates the fader, whose sends go to <see cref="OnCommit"/> and whose redraws are asked for when a send changes what is shown.</summary>
    public Fader()
    {
        _sender = new ValueSender(value => OnCommit.InvokeAsync(value), () => StateHasChanged());
    }

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

    private bool Interactive => OnCommit.HasDelegate;

    private double Travel => _gesture?.Position ?? _sender.Held ?? Math.Clamp(Position, 0, 1);

    private string Percent => (Travel * 100).ToString("0.##", CultureInfo.InvariantCulture);

    private string Now => Math.Round(Travel * 100).ToString(CultureInfo.InvariantCulture);

    private string Shown => Format is { } format && (_gesture is not null || _sender.Held is not null) ? format(Travel) : Text;

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
        _sender.Dispose();
        if (_gesture is { Active: true })
        {
            Pointer.Stop();
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The new position has been drawn, so the preview that stood in for it can go.
        if (_gesture is null)
        {
            _sender.Arrived(Math.Clamp(Position, 0, 1));
        }
    }

    private Task PressAsync(PointerEventArgs press)
    {
        if (!Interactive || Disabled || press.Button != 0 || _gesture is not null)
        {
            return Task.CompletedTask;
        }
        ValueGesture gesture = new(Travel, press.ClientY);
        _gesture = gesture;
        Pointer.Begin(
            (x, y) => Moved(gesture, y),
            (x, y) =>
            {
                Moved(gesture, y);
                _ = InvokeAsync(() => ReleaseAsync(gesture));
            },
            () => Cancel(gesture));
        return Task.CompletedTask;
    }

    private void Moved(ValueGesture gesture, double y)
    {
        gesture.Move(y, Pointer.Shift);
        _ = InvokeAsync(StateHasChanged);
    }

    private void Cancel(ValueGesture gesture)
    {
        if (ReferenceEquals(_gesture, gesture))
        {
            _gesture = null;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private async Task ReleaseAsync(ValueGesture gesture)
    {
        double? place = gesture.Commit();
        _gesture = null;
        if (place is { } target)
        {
            await CommitAsync(target);
        }
        StateHasChanged();
    }

    private Task KeyAsync(KeyboardEventArgs key)
    {
        if (Disabled || key.CtrlKey || key.MetaKey || key.AltKey)
        {
            return Task.CompletedTask;
        }
        if (Shortcuts.ControlReset.Matches(key))
        {
            return ResetAsync();
        }
        return ValueGesture.Step(key.Key, key.ShiftKey, Travel) is { } target ? CommitAsync(target) : Task.CompletedTask;
    }

    private Task ResetAsync()
    {
        return Disabled ? Task.CompletedTask : CommitAsync(Math.Clamp(DefaultPosition, 0, 1));
    }

    // The preview stays until the position that answers it is drawn; keys that arrive while one is on its way are folded into the next send.
    private Task CommitAsync(double target)
    {
        if (!_sender.Sending && Math.Abs(target - Travel) < 1e-9)
        {
            return Task.CompletedTask;
        }
        return _sender.SendAsync(target);
    }
}
