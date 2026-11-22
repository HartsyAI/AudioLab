using AudioLab.Core.Model;
using AudioLab.Server.Store;

namespace AudioLab.Server.Routes;

/// <summary>Turns the store's refusals into answers: 404 for a project the user does not have, 422 with the problem list for an invalid file, 409 for a closed session.</summary>
public sealed class StoreErrorFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ProjectNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "There is no such project.");
        }
        catch (SessionClosedException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
        catch (ProjectFormatException exception)
        {
            Dictionary<string, object?> extensions = new() { ["problems"] = exception.Problems.ToArray() };
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "The project is not valid.", extensions: extensions);
        }
    }
}
