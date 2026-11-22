namespace AudioLab.Server.Mcp;

/// <summary>What the MCP endpoint reads from configuration, under AudioLab:Mcp.</summary>
public sealed record McpOptions
{
    /// <summary>The smallest request body limit allowed, in bytes.</summary>
    public const int MinRequestBytes = 4 * 1024;

    /// <summary>The largest request body limit allowed, in bytes.</summary>
    public const int MaxRequestBytesLimit = 16 * 1024 * 1024;

    /// <summary>The smallest document limit allowed, in characters.</summary>
    public const int MinDocumentChars = 1024;

    /// <summary>The largest document limit allowed, in characters.</summary>
    public const int MaxDocumentCharsLimit = 16 * 1024 * 1024;

    /// <summary>The smallest result limit allowed, in characters.</summary>
    public const int MinResultChars = 1024;

    /// <summary>The largest result limit allowed, in characters.</summary>
    public const int MaxResultCharsLimit = 1024 * 1024;

    /// <summary>The most calls one user may have running at once.</summary>
    public const int MaxConcurrentCallsLimit = 64;

    /// <summary>The largest request body the endpoint reads, in bytes; a larger one is answered 413. A tool call carries its command.</summary>
    public int MaxRequestBytes { get; init; } = 1024 * 1024;

    /// <summary>The longest project document a resource returns, in characters; a longer one is refused.</summary>
    /// <remarks>
    /// A client has to read the whole answer, so an answer is bounded; a project can be several times this size.
    /// A tool reads less: see <see cref="MaxResultChars"/>.
    /// </remarks>
    public int MaxDocumentChars { get; init; } = 1024 * 1024;

    /// <summary>The longest result a tool returns, in characters; a longer one is refused with a <c>limit</c> error.</summary>
    /// <remarks>An agent client reads a result into its context, and Claude Code caps a tool's output at 25,000 tokens by default.</remarks>
    public int MaxResultChars { get; init; } = 80_000;

    /// <summary>How many calls one user may have running at once; a further one is answered 429, as the hub allows one per connection.</summary>
    public int MaxConcurrentCallsPerUser { get; init; } = 4;

    /// <summary>What a client is told when a document is over <see cref="MaxDocumentChars"/>.</summary>
    public string DocumentTooLarge => $"The project document is longer than the {MaxDocumentChars} characters this server returns.";
}
