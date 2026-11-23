namespace AudioLab.Server.Media;

/// <summary>An upload that is not an audio file the server accepts; the message says why and is safe to show the client.</summary>
public sealed class MediaRejectedException(string reason) : Exception(reason);
