namespace AudioLab.Core.Transcription;

/// <summary>The speech model a user uses, and the speech models installed on the server, for the speech-model route (D98).</summary>
public sealed record SpeechModelStatus(string Model, IReadOnlyList<string> Installed);
