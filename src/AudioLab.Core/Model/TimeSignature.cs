namespace AudioLab.Core.Model;

/// <summary>A signature such as 4/4. One per project.</summary>
/// <param name="Numerator">Beats per bar.</param>
/// <param name="Denominator">The note value of a beat: 1, 2, 4, 8, 16 or 32.</param>
public readonly record struct TimeSignature(int Numerator, int Denominator);
