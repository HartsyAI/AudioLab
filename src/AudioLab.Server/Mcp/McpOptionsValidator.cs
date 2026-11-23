namespace AudioLab.Server.Mcp;

/// <summary>Checks <see cref="McpOptions"/> for the server's configuration validator.</summary>
public static class McpOptionsValidator
{
    /// <summary>Adds a line to <paramref name="problems"/> for each limit that is out of range.</summary>
    public static void Validate(McpOptions options, List<string> problems)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(problems);
        if (options.MaxRequestBytes is < McpOptions.MinRequestBytes or > McpOptions.MaxRequestBytesLimit)
        {
            problems.Add($"AudioLab:Mcp:MaxRequestBytes must be {McpOptions.MinRequestBytes} to {McpOptions.MaxRequestBytesLimit}.");
        }
        if (options.MaxDocumentChars is < McpOptions.MinDocumentChars or > McpOptions.MaxDocumentCharsLimit)
        {
            problems.Add($"AudioLab:Mcp:MaxDocumentChars must be {McpOptions.MinDocumentChars} to {McpOptions.MaxDocumentCharsLimit}.");
        }
        if (options.MaxResultChars is < McpOptions.MinResultChars or > McpOptions.MaxResultCharsLimit)
        {
            problems.Add($"AudioLab:Mcp:MaxResultChars must be {McpOptions.MinResultChars} to {McpOptions.MaxResultCharsLimit}.");
        }
        if (options.MaxConcurrentCallsPerUser is < 1 or > McpOptions.MaxConcurrentCallsLimit)
        {
            problems.Add($"AudioLab:Mcp:MaxConcurrentCallsPerUser must be 1 to {McpOptions.MaxConcurrentCallsLimit}.");
        }
    }
}
