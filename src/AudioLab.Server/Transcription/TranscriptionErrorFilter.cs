namespace AudioLab.Server.Transcription;

/// <summary>Turns the transcription refusals into answers: 404 no job or transcript, 409 a state the request does not allow,
/// 422 a transcript not accepted, 429 the limit.</summary>
/// <remarks>Store failures and a missing media file are answered by the media filter, which runs inside this one.</remarks>
public sealed class TranscriptionErrorFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (TranscriptionNotFoundException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: exception.Message);
        }
        catch (TranscriptionConflictException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
        catch (TranscriptionLimitException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: exception.Message);
        }
        catch (SpeechModelChoiceException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: exception.Message);
        }
        catch (TranscriptRefusedException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The transcript is not accepted.",
                detail: string.Join(" ", exception.Problems.Take(10)));
        }
    }
}
