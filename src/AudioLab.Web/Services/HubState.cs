namespace AudioLab.Web.Services;

/// <summary>Where the connection to the project hub stands.</summary>
public enum HubState
{
    /// <summary>Not connected, and not trying to be.</summary>
    Disconnected,

    /// <summary>Opening the connection.</summary>
    Connecting,

    /// <summary>Connected; a project may be joined.</summary>
    Connected,

    /// <summary>The connection dropped and is being reopened; every joined project must be joined again afterwards.</summary>
    Reconnecting,
}
