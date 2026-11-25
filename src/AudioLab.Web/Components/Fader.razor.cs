using System.Globalization;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue channel fader: a slot, a cap and the decibel scale at its left; with <see cref="OnCommit"/> it is a slider you drag, step and reset.</summary>
public sealed partial class Fader : IDisposable
{
    private const int SettleMilliseconds = 400;

    private ValueGesture? _gesture;

    private double? _held;

    private CancellationTokenSource? _settle;

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

    private double Travel => _gesture?.Position ?? _held ?? Math.Clamp(Position, 0, 1);

    private string Percent => (Travel * 100).ToString("0.##", CultureInfo.InvariantCulture);

    private string Now => Math.Round(Travel * 100).ToString(CultureInfo.InvariantCulture);

    private string Shown => Format is { } format && (_gesture is not null || _held is not null) ? format(Travel) : Text;

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
        _settle?.Cancel();
        _settle?.Dispose();
        if (_gesture is { Active: true })
        {
            Pointer.Stop();
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The new position has been drawn, so the preview that stood in for it can go.
        if (_held is not null && _gesture is null && Math.Abs(Math.Clamp(Position, 0, 1) - _held.Value) < 1e-9)
        {
            _held = null;
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

    private async Task CommitAsync(double target)
    {
        if (Math.Abs(target - Travel) < 1e-9)
        {
            return;
        }
        _held = target;
        StateHasChanged();
        await OnCommit.InvokeAsync(target);
        await LetGoAsync();
    }

    // A refused edit never moves the position, so the preview must not stay for ever: it goes after a moment if the position did not come.
    private async Task LetGoAsync()
    {
        _settle?.Cancel();
        _settle?.Dispose();
        CancellationTokenSource settle = new();
        _settle = settle;
        try
        {
            await Task.Delay(SettleMilliseconds, settle.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        _held = null;
        StateHasChanged();
    }
}
