using AudioLab.Server.Decoding;
using AudioLab.Server.Mcp;
using AudioLab.Server.Media;
using AudioLab.Server.Midi;
using AudioLab.Server.Render;
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

    /// <summary>What the media store accepts and keeps.</summary>
    public MediaOptions Media { get; init; } = new();

    /// <summary>What a render of a project may use.</summary>
    public RenderOptions Render { get; init; } = new();

    /// <summary>What the MIDI import and export routes accept.</summary>
    public MidiOptions Midi { get; init; } = new();

    /// <summary>What the optional audio decoder may do, for compressed uploads.</summary>
    public DecoderOptions Decoder { get; init; } = new();

    /// <summary>The largest request body a route accepts, in bytes, unless the route sets its own limit as the media upload does.</summary>
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
