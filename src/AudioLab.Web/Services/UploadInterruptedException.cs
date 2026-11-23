namespace AudioLab.Web.Services;

/// <summary>A media upload ended without an answer: the connection closed or failed before the server said anything.</summary>
/// <remarks>The server may refuse a file for its size or for the user's quota before it reads the body, and a client can see that as a closed connection.</remarks>
public sealed class UploadInterruptedException(string message, Exception inner) : Exception(message, inner);
