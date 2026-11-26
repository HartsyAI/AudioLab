using AudioLab.Core.Model;

namespace AudioLab.Core.Transcription;

/// <summary>One segment of a model's answer for a window, with times in seconds from the start of that window.</summary>
/// <param name="Start">Where the segment starts, in seconds from the window's start.</param>
/// <param name="End">Where it ends, in seconds from the window's start.</param>
/// <param name="Text">The text, up to <see cref="TranscriptionLimits.MaxTextChars"/> characters.</param>
/// <param name="Kind">Whether the words are spoken, sung or something else.</param>
/// <param name="Words">The word times, when the model gives them; in seconds from the window's start.</param>
public sealed record SpeechSegment(double Start, double End, string Text, SpeechKind Kind, ValueList<LyricWord>? Words = null);
