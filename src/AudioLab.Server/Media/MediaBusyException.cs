namespace AudioLab.Server.Media;

/// <summary>The user already has as many uploads running as are allowed.</summary>
public sealed class MediaBusyException(int limit)
    : Exception($"You already have {limit} uploads running, the most allowed at once; wait for one to finish.");
