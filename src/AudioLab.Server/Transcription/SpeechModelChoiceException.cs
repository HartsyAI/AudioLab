namespace AudioLab.Server.Transcription;

/// <summary>A speech model a user asked for is not one the server can use (D98). Answered with 422.</summary>
public sealed class SpeechModelChoiceException(string message) : Exception(message);
