using AudioLab.Server.Mcp;
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

    /// <summary>The longest command the hub accepts, in characters; a longer one is refused with a <c>limit</c> error and the connection stays.</summary>
    /// <remarks>
    /// Core allows a command of 16 Mi characters, far more than any edit needs. The hub carries a command as a string inside
    /// a JSON message, and JSON escaping can make a character up to six bytes, so the largest message it reads is
    /// <see cref="HubMaxMessageBytes"/>; a bigger message closes the connection, which SignalR cannot answer in any other way.
    /// </remarks>
    public int HubMaxCommandChars { get; init; } = 2 * 1024 * 1024;

    /// <summary>The largest message the hub reads from a client: a command of <see cref="HubMaxCommandChars"/> characters at six bytes each, and the rest of the call.</summary>
    public long HubMaxMessageBytes => ((long)HubMaxCommandChars * 6) + 4096;

    /// <summary>The longest one message to a client may take to be handed to its connection; a client that takes longer is disconnected.</summary>
    public TimeSpan HubSendTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The MCP endpoint's limits.</summary>
    public McpOptions Mcp { get; init; } = new();

    /// <summary>The path the site is served under, such as /audiolab, or empty for the root.</summary>
    public string PathBase { get; init; } = string.Empty;
}
