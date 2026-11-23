using System.Net;

namespace AudioLab.Web.Services;

/// <summary>The server answered a route and refused it; the message says why and is fit to show to the user.</summary>
public sealed class ApiException : Exception
{
    /// <summary>Creates the exception for an answer.</summary>
    /// <param name="status">The HTTP status the server answered with.</param>
    /// <param name="message">What the server said went wrong.</param>
    /// <param name="retryAfter">How long the server asked the client to wait before asking again, when it said.</param>
    public ApiException(HttpStatusCode status, string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        Status = status;
        RetryAfter = retryAfter;
    }

    /// <summary>The HTTP status the server answered with.</summary>
    public HttpStatusCode Status { get; }

    /// <summary>How long the server asked the client to wait before asking again (its Retry-After), or null when it did not say.</summary>
    public TimeSpan? RetryAfter { get; }
}
