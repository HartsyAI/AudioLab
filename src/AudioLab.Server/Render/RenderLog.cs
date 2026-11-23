namespace AudioLab.Server.Render;

/// <summary>The log messages of rendering. File names are the media store's own hashed names, never an id or a user's text.</summary>
internal static partial class RenderLog
{
    [LoggerMessage(EventId = 300, Level = LogLevel.Warning, Message = "The media file {File} was not used by a render: {Reason}")]
    public static partial void MediaUnusable(ILogger logger, string file, string reason);

    [LoggerMessage(EventId = 301, Level = LogLevel.Warning, Message = "An export had {Clipped} samples clipped and {NonFinite} not finite.")]
    public static partial void ExportClipped(ILogger logger, long clipped, long nonFinite);

    [LoggerMessage(EventId = 302, Level = LogLevel.Information, Message = "An export was stopped after its first byte: {Reason}")]
    public static partial void ExportAborted(ILogger logger, string reason);

    [LoggerMessage(EventId = 303, Level = LogLevel.Error, Message = "An export failed after its first byte.")]
    public static partial void ExportFailed(ILogger logger, Exception exception);
}
