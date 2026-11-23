using System.Net;

namespace AudioLab.Web.Services;

/// <summary>The server answered a route and refused it; the message says why and is fit to show to the user.</summary>
public sealed class ApiException : Exception
{
    /// <summary>Creates the exception for an answer.</summary>
    /// <param name="status">The HTTP status the server answered with.</param>
    /// <param name="message">What the server said went wrong.</param>
    public ApiException(HttpStatusCode status, string message)
        : base(message)
    {
        Status = status;
    }

    /// <summary>The HTTP status the server answered with.</summary>
    public HttpStatusCode Status { get; }
}
