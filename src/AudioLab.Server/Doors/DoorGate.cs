namespace AudioLab.Server.Doors;

/// <summary>Asks a door about a request and records the answer; the one place a route filter and the hub's middleware share.</summary>
public static class DoorGate
{
    /// <summary>Resolves the request's user into the request, or returns the refusal to send.</summary>
    public static IResult? Admit(IDoor door, ILogger logger, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(door);
        ArgumentNullException.ThrowIfNull(http);
        DoorResult result = door.Identify(new DoorRequest(http.Request.Headers, http.Connection.RemoteIpAddress, http.Request.Scheme));
        if (result.User is not { } user)
        {
            DoorLog.Denied(logger, door.Name, result.StatusCode, result.Reason ?? string.Empty);
            return Results.Problem(statusCode: result.StatusCode);
        }
        http.SetUser(user);
        return null;
    }
}
