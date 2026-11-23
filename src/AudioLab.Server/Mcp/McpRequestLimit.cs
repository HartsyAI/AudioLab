using Microsoft.AspNetCore.Http.Features;

namespace AudioLab.Server.Mcp;

/// <summary>Limits the body of a request to the MCP endpoint and answers 413 when it is longer, as the routes do.</summary>
public static class McpRequestLimit
{
    /// <summary>Wraps <paramref name="next"/> so a body over <paramref name="maxBytes"/> is refused, however its length is found.</summary>
    public static RequestDelegate Wrap(RequestDelegate next, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(next);
        return async context =>
        {
            if (context.Request.ContentLength > maxBytes)
            {
                await TooLargeAsync(context);
                return;
            }
            context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = maxBytes;
            try
            {
                await next(context);
            }
            catch (BadHttpRequestException exception) when (IsTooLarge(exception) && !context.Response.HasStarted)
            {
                await TooLargeAsync(context);
            }
        };
    }

    private static bool IsTooLarge(BadHttpRequestException exception) => exception.StatusCode == StatusCodes.Status413PayloadTooLarge;

    private static Task TooLargeAsync(HttpContext context)
    {
        IResult refusal = Results.Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "The request is longer than this server reads.");
        return refusal.ExecuteAsync(context);
    }
}
