namespace AudioLab.Core.Transcription;

/// <summary>One speech model the server knows (D98): its id, its capability, its folder under the models root and its language.</summary>
public sealed record SpeechModelEntry(string Id, string Capability, string Folder, string Language);
