using AudioLab.Server.Store;
using Microsoft.AspNetCore.Connections;

namespace AudioLab.Server.Media;

/// <summary>Turns the media store's refusals into answers: 404 not the user's, 413 too large, 422 not accepted, 409 full, 429 busy.</summary>
public sealed class MediaErrorFilter(ILogger<MediaErrorFilter> logger) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (MediaNotFoundException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: exception.Message);
        }
        catch (MediaTooLargeException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: exception.Message);
        }
        catch (BadHttpRequestException exception)
        {
            // The request itself was bad or too large (413), cut short or too slow (400, 408): the client's doing, not the store's.
            string title = exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? "The file is larger than the server accepts."
                : "The request could not be read as sent.";
            return Results.Problem(statusCode: exception.StatusCode, title: title);
        }
        catch (ConnectionResetException)
        {
            // The client went away mid-upload; there is no one to answer and nothing wrong with the store.
            return Results.StatusCode(StatusCodes.Status400BadRequest);
        }
        catch (MediaBusyException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: exception.Message);
        }
        catch (MediaRejectedException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The file is not accepted.",
                detail: exception.Message);
        }
        catch (Exception exception) when (exception is MediaQuotaExceededException or MediaInUseException or MediaDeleteInProgressException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StoreLog.IoFailed(logger, exception.GetType().Name, exception);
            return StoreIoErrors.IsDiskFull(exception)
                ? Results.Problem(statusCode: StatusCodes.Status507InsufficientStorage, title: "The server has no room left to store media.")
                : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The media store is not available; try again.");
        }
    }
}
