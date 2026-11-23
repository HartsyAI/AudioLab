namespace AudioLab.Audio;

/// <summary>What an insert needs to know about the project it runs in.</summary>
/// <param name="SampleRate">The rate the engine renders at, in hertz.</param>
/// <param name="TempoSteps">The tempo from frame 0 on, in frame order; the first step starts at frame 0.</param>
public sealed record InsertContext(int SampleRate, IReadOnlyList<TempoStep> TempoSteps);
