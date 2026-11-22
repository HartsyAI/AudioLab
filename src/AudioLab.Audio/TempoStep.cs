namespace AudioLab.Audio;

/// <summary>The tempo from a timeline frame on, until the next step.</summary>
/// <param name="Frame">The first frame the tempo holds on.</param>
/// <param name="Bpm">The tempo, in beats per minute.</param>
public readonly record struct TempoStep(long Frame, double Bpm);
