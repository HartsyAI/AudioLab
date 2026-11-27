namespace AudioLab.Server.Decoding;

/// <summary>What an install request did, for the route to answer.</summary>
public enum DecoderInstallOutcome
{
    /// <summary>The download has started; poll the status.</summary>
    Started,

    /// <summary>The decoder is already installed and verified; nothing was downloaded.</summary>
    AlreadyInstalled,

    /// <summary>An install is already running.</summary>
    Busy,

    /// <summary>The install route is turned off for this server.</summary>
    Disabled,

    /// <summary>No decoder build is pinned for this platform.</summary>
    Unsupported,

    /// <summary>The version or checksum sent is not the pinned build's, so the user did not confirm this one.</summary>
    NotConfirmed,
}
