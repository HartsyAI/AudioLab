using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Bar and click arithmetic for a time signature, counted in quarter notes.</summary>
public static class MeterRules
{
    /// <summary>How many quarter notes a bar holds: 6/8 is 3, 7/8 is 3.5.</summary>
    public static double BeatsPerBar(TimeSignature signature) => signature.Numerator * 4.0 / signature.Denominator;

    /// <summary>The quarter notes between clicks.</summary>
    /// <remarks>A bar of whole quarters clicks on each quarter; a fractional bar (7/8) clicks on its denominator unit, so every downbeat lands on a click.</remarks>
    public static double ClickUnit(TimeSignature signature)
    {
        double bar = BeatsPerBar(signature);
        return bar == Math.Floor(bar) ? 1 : 4.0 / signature.Denominator;
    }
}
