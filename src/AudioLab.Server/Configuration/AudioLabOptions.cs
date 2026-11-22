using AudioLab.Server.Store;

namespace AudioLab.Server.Configuration;

/// <summary>Everything the server reads from configuration, under the AudioLab section.</summary>
public sealed record AudioLabOptions
{
    /// <summary>The name of the configuration section.</summary>
    public const string Section = "AudioLab";

    /// <summary>The door this instance serves.</summary>
    public DoorOptions Door { get; init; } = new();

    /// <summary>Where projects are stored and how they autosave.</summary>
    public StoreOptions Storage { get; init; } = new();

    /// <summary>The largest request body any route accepts, in bytes. Every body today is a small JSON object; a route that takes uploads will need its own limit.</summary>
    public long MaxRequestBodyBytes { get; init; } = 64 * 1024;

    /// <summary>The largest message the hub accepts from a client, in bytes; a command is far smaller.</summary>
    public int HubMaxMessageBytes { get; init; } = 1024 * 1024;

    /// <summary>The path the site is served under, such as /audiolab, or empty for the root.</summary>
    public string PathBase { get; init; } = string.Empty;
}
