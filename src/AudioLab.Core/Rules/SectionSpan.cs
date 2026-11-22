namespace AudioLab.Core.Rules;

/// <summary>Where a section sits: seconds from the start of the project.</summary>
/// <param name="Start">Where it starts.</param>
/// <param name="End">Where it ends.</param>
public readonly record struct SectionSpan(double Start, double End);
