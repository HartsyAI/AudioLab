namespace AudioLab.Web.Media;

/// <summary>The log messages of the media library, written to the browser's console.</summary>
internal static partial class MediaLog
{
    [LoggerMessage(EventId = 300, Level = LogLevel.Warning, Message = "An upload failed.")]
    public static partial void UploadFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 301, Level = LogLevel.Warning, Message = "The media list could not be read.")]
    public static partial void ListFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 302, Level = LogLevel.Information, Message = "The media limits could not be read; the server will judge each file.")]
    public static partial void LimitsFailed(ILogger logger, Exception exception);
}
