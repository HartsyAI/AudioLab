namespace AudioLab.Core;

/// <summary>The contract between the AudioLab server and whatever drives it: the web UI, the Swarm extension, an MCP client.</summary>
public static class AudioLabApi
{
    /// <summary>Contract version. Raise it for any breaking change to a route, a command or an event.</summary>
    public const int Version = 1;
}
