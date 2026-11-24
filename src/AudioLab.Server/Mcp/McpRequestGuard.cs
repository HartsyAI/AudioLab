using AudioLab.Server.Doors;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace AudioLab.Server.Mcp;

/// <summary>Checks a request to the MCP endpoint after the door has admitted it, whichever door that is.</summary>
/// <remarks>
/// An agent is not a browser, but a page the user has open can still send a request to the endpoint. A POST of
/// <c>text/plain</c> is a simple request, which a browser sends without asking first, so the endpoint refuses a content
/// type other than JSON, a browser's mark of another origin or another site, and a user with too many calls in flight.
/// </remarks>
public static class McpRequestGuard
{
    /// <summary>The one media type a message may have.</summary>
    public const string JsonMediaType = "application/json";

    /// <summary>Wraps <paramref name="next"/> with the checks and the size limit.</summary>
    public static RequestDelegate Wrap(RequestDelegate next, McpOptions options, McpCallLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(limiter);
        RequestDelegate limited = McpRequestLimit.Wrap(next, options.MaxRequestBytes);
        return async context =>
        {
            if (Refusal(context.Request) is { } refusal)
            {
                await refusal.ExecuteAsync(context);
                return;
            }
            using IDisposable? slot = limiter.TryEnter(context.RequireUser());
            if (slot is null)
            {
                context.Response.Headers.RetryAfter = "1";
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "This user has too many MCP calls running; try again.")
                    .ExecuteAsync(context);
                return;
            }
            await limited(context);
        };
    }

    private static IResult? Refusal(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Origin", out StringValues origins) && !IsOwnOrigin(origins, request))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "The request comes from another origin.");
        }
        if (request.Headers.TryGetValue("Sec-Fetch-Site", out StringValues sites) && !(sites.Count == 1 && sites[0] is "same-origin" or "none"))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "The browser marks the request as coming from another site.");
        }
        if (HttpMethods.IsPost(request.Method) && !IsJson(request.ContentType))
        {
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: $"The body must be {JsonMediaType}.");
        }
        return null;
    }

    // The proxy may rewrite the Host, so on a proxy door this can refuse a page that really is same-origin: it fails closed.
    private static bool IsOwnOrigin(StringValues origins, HttpRequest request)
    {
        return origins.Count == 1
            && Uri.TryCreate(origins[0], UriKind.Absolute, out Uri? origin)
            && origin.Scheme == request.Scheme
            && string.Equals(origin.Authority, new Uri($"{request.Scheme}://{request.Host}").Authority, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJson(string? contentType)
    {
        return MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue? parsed)
            && string.Equals(parsed.MediaType.Value, JsonMediaType, StringComparison.OrdinalIgnoreCase);
    }
}
