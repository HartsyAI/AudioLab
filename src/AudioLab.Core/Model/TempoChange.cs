namespace AudioLab.Core.Model;

/// <summary>A change of tempo at a point in time. The project tempo runs from zero until the first change.</summary>
/// <param name="Id">The change's id.</param>
/// <param name="Time">Where it starts, in seconds from the start of the project. Always above zero.</param>
/// <param name="Bpm">The tempo from there on, in beats per minute.</param>
public readonly record struct TempoChange(TempoChangeId Id, double Time, double Bpm);
