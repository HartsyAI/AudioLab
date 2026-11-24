namespace AudioLab.Core.Rules;

/// <summary>What a numeric effect setting measures, which says how a value is written beside its control.</summary>
public enum EffectUnit
{
    /// <summary>A plain number with no unit.</summary>
    None,

    /// <summary>A frequency in hertz.</summary>
    Hertz,

    /// <summary>A level or a gain in decibels.</summary>
    Decibel,

    /// <summary>A time in seconds.</summary>
    Seconds,

    /// <summary>A ratio of the input change to the output change, written such as 4:1.</summary>
    Ratio,

    /// <summary>A share from 0 to 1, written as a percentage.</summary>
    Fraction,
}
