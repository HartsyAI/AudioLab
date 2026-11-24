namespace AudioLab.Core.Rules;

/// <summary>How a control spreads a setting's range along its travel.</summary>
public enum EffectScale
{
    /// <summary>Equal steps of the value for equal travel.</summary>
    Linear,

    /// <summary>Equal ratios of the value for equal travel, for frequencies and times; the range starts above zero.</summary>
    Logarithmic,
}
