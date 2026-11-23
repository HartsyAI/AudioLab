using AudioLab.Core.Rules;

namespace AudioLab.Audio;

/// <summary>What an insert needs to know about the project it runs in.</summary>
/// <param name="SampleRate">The rate the engine renders at, in hertz.</param>
/// <param name="Tempo">The project's tempo map, for effects that follow the tempo.</param>
public sealed record InsertContext(int SampleRate, TempoMap Tempo);
