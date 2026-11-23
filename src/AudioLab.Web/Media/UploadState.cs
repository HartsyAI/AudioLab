namespace AudioLab.Web.Media;

/// <summary>Where an upload is: waiting for a place, being handed to the browser, waiting for the server's answer, or refused.</summary>
public enum UploadState
{
    /// <summary>Waiting for another upload to finish; the server takes only a few at once.</summary>
    Queued,

    /// <summary>The page is reading the file and handing it to the browser; the progress is the share read.</summary>
    Reading,

    /// <summary>The file is with the browser and the server has not answered; there is no figure for this wait.</summary>
    Sending,

    /// <summary>The upload ended without a file; <see cref="UploadJob.Failure"/> says why.</summary>
    Failed,
}
