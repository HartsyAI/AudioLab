using System.Globalization;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue knob: a disc with a pointer and its value; with <see cref="OnCommit"/> it is a slider you drag, step and reset.</summary>
public sealed partial class Knob : IDisposable
{
    private const double SweepDegrees = 135;

    private const int SettleMilliseconds = 400;

    private ValueGesture? _gesture;

    private double? _held;

    private double _wanted;

    private double _sent = double.NaN;

    private bool _sending;

    private CancellationTokenSource? _settle;

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

    private bool Interactive => OnCommit.HasDelegate;

    // The place on the travel from 0 to 1 that is drawn: the drag or the key being answered, else the setting.
    private double Travel => _gesture?.Position ?? _held ?? ((Math.Clamp(Position, -1, 1) + 1) / 2);

    private string Transform => $"rotate({(((Travel * 2) - 1) * SweepDegrees).ToString("0.#", CultureInfo.InvariantCulture)} 12 12)";

    private string Now => Math.Round(Travel * 100).ToString(CultureInfo.InvariantCulture);

    private string Shown => Format is { } format && (_gesture is not null || _held is not null) ? format((Travel * 2) - 1) : Text;

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
        _settle?.Cancel();
        _settle?.Dispose();
        if (_gesture is { } gesture)
        {
            Pointer.Stop();
            if (gesture.Active)
            {
                _ = OnAbandon.InvokeAsync();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The new setting has been drawn, so the preview that stood in for it can go.
        if (_held is not null && !_sending && _gesture is null && Math.Abs(((Math.Clamp(Position, -1, 1) + 1) / 2) - _held.Value) < 1e-9)
        {
            _held = null;
        }
    }

    private Task PressAsync(PointerEventArgs press)
    {
        if (Disabled || press.Button != 0 || _gesture is not null)
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
        return Disabled ? Task.CompletedTask : CommitAsync((Math.Clamp(DefaultPosition, -1, 1) + 1) / 2);
    }

    // The preview stays until the setting that answers it is drawn; keys that arrive while one is on its way are folded into the next send.
    private async Task CommitAsync(double target)
    {
        if (!_sending && Math.Abs(target - Travel) < 1e-9)
        {
            // Home at the lowest value, End at the highest, Backspace at the default: nothing changes, so nothing is sent and undo is left alone.
            return;
        }
        _held = target;
        _wanted = target;
        StateHasChanged();
        if (_sending)
        {
            return;
        }
        _sending = true;
        try
        {
            while (_sent != _wanted)
            {
                _sent = _wanted;
                await OnCommit.InvokeAsync((_sent * 2) - 1);
            }
        }
        finally
        {
            _sending = false;
            _sent = double.NaN;
        }
        await LetGoAsync();
    }

    // A refused edit never changes the setting, so the preview must not stay for ever: it goes after a moment if the setting did not come.
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
