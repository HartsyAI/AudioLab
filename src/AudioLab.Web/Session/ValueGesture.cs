namespace AudioLab.Web.Session;

/// <summary>A drag or a key press on a control with a travel from 0 to 1: it works out where the travel ends up and sends nothing.</summary>
/// <remarks>
/// A drag follows the pointer's moves, not its distance from the press, so holding Shift part way through slows the rest of the drag without a
/// jump. The result is an absolute place on the travel, which the caller turns into a value and sends once, on release.
/// </remarks>
public sealed class ValueGesture
{
    /// <summary>The pixels of vertical travel that cover the whole range.</summary>
    public const double TravelPixels = 160;

    /// <summary>How much of a normal movement a fine one is.</summary>
    public const double FineFactor = 0.1;

    /// <summary>The pixels the pointer must travel before a press is a drag and not a click.</summary>
    public const double Threshold = 3;

    /// <summary>The size of an arrow key's step, as a share of the travel.</summary>
    public const double KeyStep = 0.01;

    /// <summary>The size of a page key's step, as a share of the travel.</summary>
    public const double PageStep = 0.1;

    private readonly double _start;

    private readonly double _startY;

    private double _lastY;

    /// <summary>Starts at a press <paramref name="y"/> pixels down the page, on a control at <paramref name="position"/> of its travel.</summary>
    public ValueGesture(double position, double y)
    {
        _start = Math.Clamp(position, 0, 1);
        Position = _start;
        _startY = y;
        _lastY = y;
    }

    /// <summary>Where the travel would end up if released now, from 0 to 1.</summary>
    public double Position { get; private set; }

    /// <summary>Whether the pointer has travelled far enough for this to be a drag.</summary>
    public bool Active { get; private set; }

    /// <summary>Moves the pointer to <paramref name="y"/> pixels down the page; up is more.</summary>
    /// <param name="y">The pointer's height.</param>
    /// <param name="fine">Whether the movement counts a tenth as much.</param>
    public void Move(double y, bool fine)
    {
        Active |= Math.Abs(y - _startY) >= Threshold;
        if (Active)
        {
            Position = Math.Clamp(Position + (((_lastY - y) / TravelPixels) * (fine ? FineFactor : 1)), 0, 1);
        }
        _lastY = y;
    }

    /// <summary>Releases: the place to send, or null when it ended where it began, so undo is left as it was.</summary>
    public double? Commit() => Active && Position != _start ? Position : null;

    /// <summary>Where a key puts a control that is at <paramref name="position"/>, or null when the key is not one that moves it.</summary>
    /// <param name="key">The key as the browser names it.</param>
    /// <param name="fine">Whether Shift is held, which makes an arrow key's step a tenth as big.</param>
    /// <param name="position">Where the control is now, from 0 to 1.</param>
    public static double? Step(string key, bool fine, double position)
    {
        ArgumentNullException.ThrowIfNull(key);
        double small = KeyStep * (fine ? FineFactor : 1);
        double? target = key switch
        {
            "ArrowUp" or "ArrowRight" => position + small,
            "ArrowDown" or "ArrowLeft" => position - small,
            "PageUp" => position + PageStep,
            "PageDown" => position - PageStep,
            "Home" => 0,
            "End" => 1,
            _ => null,
        };
        return target is { } place ? Math.Clamp(place, 0, 1) : null;
    }
}
