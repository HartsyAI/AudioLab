namespace AudioLab.Audio.Live;

/// <summary>A clip the engine needs cannot be had from where it is kept; the message says why and leaves the clip to the caller to name.</summary>
public sealed class ClipUnavailableException(string reason) : Exception(reason);
