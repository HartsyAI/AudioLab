namespace AudioLab.Web.Waveforms;

/// <summary>The log messages of the peak store, written to the browser's console.</summary>
internal static partial class PeakLog
{
    [LoggerMessage(EventId = 310, Level = LogLevel.Warning, Message = "A request for the peaks of a media file failed.")]
    public static partial void FetchFailed(ILogger logger, Exception exception);
}
