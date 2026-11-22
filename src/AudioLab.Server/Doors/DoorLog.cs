namespace AudioLab.Server.Doors;

/// <summary>The log messages of the doors. None carries a header value.</summary>
internal static partial class DoorLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "The {Door} door refused a request with {StatusCode}: {Reason}.")]
    public static partial void Denied(ILogger logger, string door, int statusCode, string reason);
}
