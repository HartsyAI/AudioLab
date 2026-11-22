namespace AudioLab.Server.Configuration;

/// <summary>The door a server instance serves; it decides how a request names its user.</summary>
public enum DoorKind
{
    /// <summary>The desktop app: loopback only, one local user, no sign-in.</summary>
    Desktop,

    /// <summary>A SwarmUI tab: the extension's proxy names the user and proves itself with a per-launch secret.</summary>
    Swarm,

    /// <summary>A hosted site: its reverse proxy names the user and proves itself with a shared secret.</summary>
    Hosted,
}
