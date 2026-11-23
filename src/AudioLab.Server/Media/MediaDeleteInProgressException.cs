namespace AudioLab.Server.Media;

/// <summary>Another delete of the file has begun and may still be refused because a project plays it.</summary>
public sealed class MediaDeleteInProgressException() : Exception("Another delete of this file is in progress; try again in a moment.");
