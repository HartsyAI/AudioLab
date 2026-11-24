namespace AudioLab.Server.Media;

/// <summary>The user has no such media file. A file of another user is reported the same way (D12).</summary>
public sealed class MediaNotFoundException() : Exception("There is no such media file.");
