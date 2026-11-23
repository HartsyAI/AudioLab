namespace AudioLab.Server.Services;

/// <summary>A user or the server is already running as many jobs of a kind as are allowed; the message says which and is safe to show.</summary>
public sealed class GateBusyException(string message) : Exception(message);
