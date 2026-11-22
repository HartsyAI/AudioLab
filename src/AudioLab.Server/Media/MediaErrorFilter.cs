using AudioLab.Server.Store;

namespace AudioLab.Server.Media;

/// <summary>Turns the media store's refusals into answers: 404 not the user's, 413 too large, 422 not accepted, 409 full or in use.</summary>
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
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "The file is larger than the server accepts.");
        }
        catch (MediaRejectedException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The file is not accepted.",
                detail: exception.Message);
        }
        catch (Exception exception) when (exception is MediaQuotaExceededException or MediaInUseException)
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
