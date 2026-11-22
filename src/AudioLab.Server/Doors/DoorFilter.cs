namespace AudioLab.Server.Doors;

/// <summary>Resolves the user of every request in a route group, or answers with the door's refusal.</summary>
public sealed class DoorFilter(IDoor door, ILogger<DoorFilter> logger) : IEndpointFilter
{
    /// <inheritdoc />
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext http = context.HttpContext;
        DoorResult result = door.Identify(new DoorRequest(http.Request.Headers, http.Connection.RemoteIpAddress, http.Request.Scheme));
        if (result.User is not { } user)
        {
            DoorLog.Denied(logger, door.Name, result.StatusCode, result.Reason ?? string.Empty);
            return ValueTask.FromResult<object?>(Results.Problem(statusCode: result.StatusCode));
        }
        http.SetUser(user);
        return next(context);
    }
}
