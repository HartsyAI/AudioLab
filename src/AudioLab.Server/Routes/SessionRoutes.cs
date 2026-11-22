using AudioLab.Server.Doors;
using AudioLab.Server.Services;

namespace AudioLab.Server.Routes;

/// <summary>The route that tells a caller who the server takes it to be.</summary>
public static class SessionRoutes
{
    /// <summary>Maps GET /session into <paramref name="routes"/>, a group behind the door filter.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/session", (HttpContext http, SessionInfoService sessions) => Results.Ok(sessions.Describe(http.RequireUser())));
    }
}
