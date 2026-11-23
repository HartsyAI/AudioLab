namespace AudioLab.Server.Render;

/// <summary>The log messages of rendering. File names are the media store's own hashed names, never an id or a user's text.</summary>
internal static partial class RenderLog
{
    [LoggerMessage(EventId = 300, Level = LogLevel.Warning, Message = "The media file {File} was not used by a render: {Reason}")]
    public static partial void MediaUnusable(ILogger logger, string file, string reason);
}
