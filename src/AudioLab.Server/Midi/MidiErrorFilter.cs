using System.Globalization;
using AudioLab.Core.Commands;
using AudioLab.Server.Services;
using Microsoft.AspNetCore.Connections;

namespace AudioLab.Server.Midi;

/// <summary>Turns the MIDI routes' refusals into answers: 404 not the user's, 413 too large, 422 not accepted, 429 busy.</summary>
public sealed class MidiErrorFilter : IEndpointFilter
{
    private const int RetryAfterSeconds = 5;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (MidiRefusedException exception)
        {
            return Results.Problem(statusCode: StatusOf(exception.Code), title: exception.Message, extensions: ReasonOf(exception.Code));
        }
        catch (GateBusyException exception)
        {
            context.HttpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: exception.Message);
        }
        catch (BadHttpRequestException exception)
        {
            // The request itself was bad or too large (413), cut short or too slow (400, 408): the client's doing, not the server's.
            string title = exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? "The file is larger than the server accepts."
                : "The request could not be read as sent.";
            return Results.Problem(statusCode: exception.StatusCode, title: title);
        }
        catch (ConnectionResetException)
        {
            // The client went away mid-upload; there is no one to answer.
            return Results.StatusCode(StatusCodes.Status400BadRequest);
        }
    }

    private static int StatusOf(CommandErrorCode code)
    {
        return code switch
        {
            CommandErrorCode.NotFound => StatusCodes.Status404NotFound,
            CommandErrorCode.Limit => StatusCodes.Status413PayloadTooLarge,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
    }

    // The names a command's refusal has on the wire, so a client reads one vocabulary.
    private static Dictionary<string, object?> ReasonOf(CommandErrorCode code)
    {
        string reason = code switch
        {
            CommandErrorCode.Malformed => "malformed",
            CommandErrorCode.UnknownCommand => "unknown_command",
            CommandErrorCode.WrongProject => "wrong_project",
            CommandErrorCode.NotFound => "not_found",
            CommandErrorCode.Rejected => "rejected",
            CommandErrorCode.Limit => "limit",
            CommandErrorCode.Invalid => "invalid",
            _ => throw new ArgumentOutOfRangeException(nameof(code)),
        };
        return new Dictionary<string, object?> { ["reason"] = reason };
    }
}
