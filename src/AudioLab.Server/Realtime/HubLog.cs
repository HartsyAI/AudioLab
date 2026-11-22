namespace AudioLab.Server.Realtime;

/// <summary>The log messages of the hub. None carries a connection id, a user or a document.</summary>
internal static partial class HubLog
{
    [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "A client did not take a message within {Milliseconds} ms; its connection was aborted.")]
    public static partial void SendTimedOut(ILogger logger, long milliseconds);

    [LoggerMessage(EventId = 201, Level = LogLevel.Warning, Message = "A send to a client failed; its connection was aborted so it joins again.")]
    public static partial void SendFailed(ILogger logger, Exception exception);
}
