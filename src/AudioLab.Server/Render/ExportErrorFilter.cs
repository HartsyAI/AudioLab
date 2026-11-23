using AudioLab.Audio;

namespace AudioLab.Server.Render;

/// <summary>Turns an export's refusals into answers, and ends a response that failed after its first byte by cutting the connection.</summary>
/// <remarks>
/// Once a byte has gone out the status is sent, so the only honest ending is to abort: a client then sees a transfer that stopped short of
/// the length it was promised, never a complete-looking file. Before the first byte the headers of the file are taken back and the
/// answer is a problem with a <c>reason</c> the client can read.
/// </remarks>
public sealed class ExportErrorFilter(ILogger<ExportErrorFilter> logger) : IEndpointFilter
{
    private const int RetryAfterSeconds = 5;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext http = context.HttpContext;
        try
        {
            return await next(context);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            RenderLog.ExportAborted(logger, "the client went away");
            return Cut(http);
        }
        catch (ExportRefusedException exception) when (!http.Response.HasStarted)
        {
            TakeBackFileHeaders(http);
            if (exception.Reason == ExportRefusal.Busy)
            {
                http.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            Dictionary<string, object?> extensions = new() { ["reason"] = ExportRefusals.Code(exception.Reason) };
            if (exception.Details.Count > 0)
            {
                extensions["details"] = exception.Details.ToArray();
            }
            return Results.Problem(statusCode: ExportRefusals.Status(exception.Reason), title: exception.Message, extensions: extensions);
        }
        catch (MissingMediaException) when (!http.Response.HasStarted)
        {
            TakeBackFileHeaders(http);
            Dictionary<string, object?> extensions = new() { ["reason"] = ExportRefusals.Code(ExportRefusal.MissingMedia) };
            return Results.Problem(
                statusCode: ExportRefusals.Status(ExportRefusal.MissingMedia),
                title: "A clip or an instrument of the project plays a media file that is not available.",
                extensions: extensions);
        }
        catch (Exception exception) when (http.Response.HasStarted)
        {
            RenderLog.ExportFailed(logger, exception);
            return Cut(http);
        }
    }

    private static IResult Cut(HttpContext http)
    {
        http.Abort();
        return Results.Empty;
    }

    private static void TakeBackFileHeaders(HttpContext http)
    {
        http.Response.Headers.ContentLength = null;
        http.Response.Headers.Remove("Content-Disposition");
        http.Response.Headers.Remove("Content-Type");
    }
}
