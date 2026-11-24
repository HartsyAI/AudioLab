namespace AudioLab.Web.Session.Effects;

/// <summary>A drag of one effect to another place in the chain: the pointer's height in the list is the place; nothing is sent early.</summary>
/// <remarks>
/// The effect commands move one place at a time (D23, D34), so a drag over several places is that many <c>effect_move</c> commands sent together as
/// one gesture. The target is a place in the list, not a distance, so a drag that went past the end and came back lands where it is released.
/// </remarks>
public sealed class EffectDrag
{
    /// <summary>The pixels the pointer must travel before a press is a drag and not a click.</summary>
    public const double Threshold = 3;

    private readonly int _count;

    private readonly double _startY;

    /// <summary>Starts at a press <paramref name="y"/> pixels from the top of a list of <paramref name="count"/> effects.</summary>
    /// <param name="effect">The id of the effect that was pressed.</param>
    /// <param name="from">Its place in the chain, from 0.</param>
    /// <param name="count">How many effects the chain has.</param>
    /// <param name="y">Where the press is, from the top of the list.</param>
    public EffectDrag(string effect, int from, int count, double y)
    {
        ArgumentException.ThrowIfNullOrEmpty(effect);
        Effect = effect;
        From = from;
        _count = count;
        _startY = y;
        Target = from;
    }

    /// <summary>The id of the effect being dragged.</summary>
    public string Effect { get; }

    /// <summary>Where it started.</summary>
    public int From { get; }

    /// <summary>Where it would land if released now.</summary>
    public int Target { get; private set; }

    /// <summary>Whether the pointer has travelled far enough for this to be a drag.</summary>
    public bool Active { get; private set; }

    /// <summary>Moves the pointer to <paramref name="y"/> pixels from the top of a list whose rows are <paramref name="pitch"/> apart.</summary>
    public void Move(double y, double pitch)
    {
        Active |= Math.Abs(y - _startY) >= Threshold;
        if (Active && pitch > 0)
        {
            Target = Math.Clamp((int)Math.Floor(y / pitch), 0, Math.Max(0, _count - 1));
        }
    }

    /// <summary>Releases: how many places to move, negative for earlier, or null when nothing changed, so undo is left as it was.</summary>
    public int? Commit()
    {
        return Active && Target != From ? Target - From : null;
    }
}
