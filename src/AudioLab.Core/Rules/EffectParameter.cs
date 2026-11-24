using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>A numeric setting of an effect: its name on the wire, how it is shown, its range, and how to read and change it.</summary>
/// <param name="Name">The camel-case name, as in the project file.</param>
/// <param name="Label">What a control is called, in sentence case.</param>
/// <param name="Unit">What the setting measures.</param>
/// <param name="Min">The lowest value the project keeps.</param>
/// <param name="Max">The highest value the project keeps.</param>
/// <param name="Get">Reads the setting from an effect of the right kind.</param>
/// <param name="Set">Returns a copy of an effect of the right kind with the setting changed.</param>
/// <param name="Scale">How a control spreads the range along its travel.</param>
/// <param name="FollowsTempo">Whether a delay that follows the tempo sets this setting itself, so it cannot be set by hand then.</param>
public sealed record EffectParameter(
    string Name,
    string Label,
    EffectUnit Unit,
    double Min,
    double Max,
    Func<Effect, double> Get,
    Func<Effect, double, Effect> Set,
    EffectScale Scale = EffectScale.Linear,
    bool FollowsTempo = false)
{
    /// <summary>Where a value sits along a control's travel, 0 at the lowest value to 1 at the highest; a value outside is held in it.</summary>
    public double ToPosition(double value)
    {
        double held = Math.Clamp(value, Min, Max);
        return Scale == EffectScale.Logarithmic
            ? Math.Log(held / Min) / Math.Log(Max / Min)
            : (held - Min) / (Max - Min);
    }

    /// <summary>The value at a place along a control's travel; a place outside 0 to 1 is held in it, and the ends are exactly the range's.</summary>
    public double FromPosition(double position)
    {
        double held = Math.Clamp(position, 0, 1);
        if (held <= 0)
        {
            return Min;
        }
        if (held >= 1)
        {
            return Max;
        }
        return Scale == EffectScale.Logarithmic ? Min * Math.Pow(Max / Min, held) : Min + ((Max - Min) * held);
    }
}
