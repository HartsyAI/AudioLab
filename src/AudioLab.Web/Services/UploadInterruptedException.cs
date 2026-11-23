namespace AudioLab.Web.Services;

/// <summary>A media upload ended without an answer: the connection closed, or the browser gave up, before the server said anything.</summary>
/// <remarks>A server may refuse a file for its size or the quota before it reads the body, which a client can see as a closed connection.</remarks>
public sealed class UploadInterruptedException(string message, Exception inner) : Exception(message, inner)
{
    /// <summary>The sentence for a connection that closed with no answer.</summary>
    public const string NoAnswer =
        "The upload stopped before the server answered. The server may have refused the file because it is too large or your storage is full.";

    /// <summary>The sentence for an upload that the browser ended without being asked to.</summary>
    public const string GaveUp = "The upload was stopped by the browser before the server answered. Check the connection and try again.";
}
