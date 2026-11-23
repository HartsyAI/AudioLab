namespace AudioLab.Server.Mcp;

/// <summary>The log messages of the MCP tools. None carries a project's content.</summary>
internal static partial class McpLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "An MCP call failed because the project store was not available.")]
    public static partial void StoreUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "An MCP call named a project whose file is not valid.")]
    public static partial void ProjectNotValid(ILogger logger, Exception exception);
}
