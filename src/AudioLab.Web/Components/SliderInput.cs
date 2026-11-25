using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The press, drag, key and reset behaviour that a knob and a fader share, over a travel from 0 to 1.</summary>
/// <remarks>
/// The control says where its setting sits on the travel. The input works out where a drag or a key puts it, and sends the place through a
/// <see cref="ValueSender"/>, so a drag is one send on release.
/// </remarks>
public sealed class SliderInput : IDisposable
{
    private readonly Func<double> _setting;

    private readonly IPointerTracker _pointer;

    private readonly Action _redraw;

    private readonly Func<Func<Task>, Task> _dispatch;

    private readonly ValueSender _sender;

    /// <summary>Starts an input for a control whose setting is <paramref name="setting"/> on the travel.</summary>
    /// <param name="setting">The setting on the travel, from 0 to 1.</param>
    /// <param name="pointer">Follows a drag once it has started.</param>
    /// <param name="redraw">Redraws the control.</param>
    /// <param name="dispatch">Runs work on the control's own thread.</param>
    /// <param name="send">Sends a place on the travel to the project.</param>
    public SliderInput(Func<double> setting, IPointerTracker pointer, Action redraw, Func<Func<Task>, Task> dispatch, Func<double, Task> send)
    {
        _setting = setting ?? throw new ArgumentNullException(nameof(setting));
        _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
        _redraw = redraw ?? throw new ArgumentNullException(nameof(redraw));
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        _sender = new ValueSender(send ?? throw new ArgumentNullException(nameof(send)), redraw);
    }

    /// <summary>The drag in progress, or null.</summary>
    public ValueGesture? Gesture { get; private set; }

    /// <summary>Where the control is drawn on the travel: the drag, else the place being sent, else the setting.</summary>
    public double Travel => Gesture?.Position ?? _sender.Held ?? _setting();

    /// <summary>Whether a drag or a sent place is shown in place of the setting.</summary>
    public bool Previewing => Gesture is not null || _sender.Held is not null;

    /// <summary>Drops the preview once the drawn setting is the place it held, and no drag is on.</summary>
    /// <param name="setting">The setting that is drawn, on the travel.</param>
    public void Arrived(double setting)
    {
        if (Gesture is null)
        {
            _sender.Arrived(setting);
        }
    }

    /// <summary>Starts a drag at a press, unless the control is blocked, the button is not the primary one, or a drag is on.</summary>
    /// <param name="press">The press.</param>
    /// <param name="blocked">Whether the control does nothing now.</param>
    public void Press(PointerEventArgs press, bool blocked)
    {
        if (blocked || press.Button != 0 || Gesture is not null)
        {
            return;
        }
        ValueGesture gesture = new(Travel, press.ClientY);
        Gesture = gesture;
        _pointer.Begin(
            (x, y) => Moved(gesture, y),
            (x, y) =>
            {
                Moved(gesture, y);
                _ = _dispatch(() => ReleaseAsync(gesture));
            },
            () => Cancel(gesture));
    }

    /// <summary>Answers a key: a modifier leaves it alone, the reset key puts the control at its default, and the others step it.</summary>
    /// <param name="key">The key.</param>
    /// <param name="blocked">Whether the control does nothing now.</param>
    /// <param name="defaultTravel">The default place on the travel, from 0 to 1.</param>
    public Task Key(KeyboardEventArgs key, bool blocked, double defaultTravel)
    {
        if (blocked || key.CtrlKey || key.MetaKey || key.AltKey)
        {
            return Task.CompletedTask;
        }
        if (Shortcuts.ControlReset.Matches(key))
        {
            return Reset(defaultTravel, blocked);
        }
        return ValueGesture.Step(key.Key, key.ShiftKey, Travel) is { } target ? Commit(target) : Task.CompletedTask;
    }

    /// <summary>Puts the control back to its default, unless it is blocked.</summary>
    /// <param name="defaultTravel">The default place on the travel, from 0 to 1.</param>
    /// <param name="blocked">Whether the control does nothing now.</param>
    public Task Reset(double defaultTravel, bool blocked)
    {
        return blocked ? Task.CompletedTask : Commit(Math.Clamp(defaultTravel, 0, 1));
    }

    /// <summary>Sends a place on the travel, unless it is where the control already is and nothing is on its way.</summary>
    /// <param name="target">The place, from 0 to 1.</param>
    public Task Commit(double target)
    {
        if (!_sender.Sending && Math.Abs(target - Travel) < 1e-9)
        {
            // Home at the lowest value, End at the highest, Backspace at the default: nothing changes, so nothing is sent and undo is left alone.
            return Task.CompletedTask;
        }
        return _sender.SendAsync(target);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sender.Dispose();
        if (Gesture is not null)
        {
            _pointer.Stop();
        }
    }

    private void Moved(ValueGesture gesture, double y)
    {
        gesture.Move(y, _pointer.Shift);
        _ = _dispatch(RedrawAsync);
    }

    private void Cancel(ValueGesture gesture)
    {
        if (ReferenceEquals(Gesture, gesture))
        {
            Gesture = null;
            _ = _dispatch(RedrawAsync);
        }
    }

    private async Task ReleaseAsync(ValueGesture gesture)
    {
        double? place = gesture.Commit();
        Gesture = null;
        if (place is { } target)
        {
            await Commit(target);
        }
        _redraw();
    }

    private Task RedrawAsync()
    {
        _redraw();
        return Task.CompletedTask;
    }
}
