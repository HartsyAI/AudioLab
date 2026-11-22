using AudioLab.Server.Services;

namespace AudioLab.Server.Routes;

/// <summary>The health route. It sits outside the door: it names no user and shows no project.</summary>
public static class HealthRoutes
{
    /// <summary>Maps GET /health.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", (HealthService health) => Results.Ok(health.Report()));
    }
}
