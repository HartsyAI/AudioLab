namespace AudioLab.Core.Transcription;

/// <summary>The audio a model is given: mono samples at <see cref="TranscriptionWindows.SampleRate"/>, and where they start in the media.</summary>
/// <param name="StartSeconds">Where the samples start, in seconds of the media.</param>
/// <param name="Samples">The mono samples, from -1 to 1.</param>
public readonly record struct SpeechWindow(double StartSeconds, ReadOnlyMemory<float> Samples);
