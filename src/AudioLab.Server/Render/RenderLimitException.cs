namespace AudioLab.Server.Render;

/// <summary>A render would go past a limit of the server; the message says which and is safe to show the client.</summary>
public sealed class RenderLimitException(string message) : Exception(message);
