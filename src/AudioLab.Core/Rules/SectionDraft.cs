namespace AudioLab.Core.Rules;

/// <summary>A section that is not in a document yet, so it has no id.</summary>
/// <param name="Start">Where it starts, in seconds.</param>
/// <param name="End">Where it ends, in seconds.</param>
/// <param name="Name">Its name.</param>
/// <param name="Color">Its colour.</param>
public readonly record struct SectionDraft(double Start, double End, string Name, string Color);
