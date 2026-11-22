using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Services;
using AudioLab.Server.Store;

namespace AudioLab.Server.Routes;

/// <summary>The project routes: parse, call <see cref="ProjectService"/>, return. They sit behind the door, so the owner is always the session's user.</summary>
public static class ProjectRoutes
{
    /// <summary>The response header that carries the revision of the document in the body.</summary>
    public const string RevisionHeader = "X-AudioLab-Revision";

    /// <summary>Maps /projects into <paramref name="routes"/>, a group behind the door filter.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder projects = routes.MapGroup("/projects").AddEndpointFilter<StoreErrorFilter>();
        projects.MapGet("/", ListAsync);
        projects.MapPost("/", CreateAsync);
        projects.MapGet("/{id}", OpenAsync);
        projects.MapPost("/{id}/save", SaveAsync);
        projects.MapDelete("/{id}", DeleteAsync);
        projects.MapPost("/{id}/duplicate", DuplicateAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext http, ProjectService service, CancellationToken cancellationToken)
    {
        return Results.Ok(await service.ListAsync(http.RequireUser(), cancellationToken));
    }

    private static async Task<IResult> CreateAsync(HttpContext http, CreateProjectRequest body, ProjectService service, CancellationToken cancellationToken)
    {
        ProjectSummary created = await service.CreateAsync(http.RequireUser(), body.Name, cancellationToken);
        return Results.Created($"{http.Request.PathBase}/api/projects/{created.Id}", created);
    }

    private static async Task<IResult> OpenAsync(HttpContext http, string id, ProjectService service, CancellationToken cancellationToken)
    {
        if (!TryParse(id, out ProjectId project))
        {
            return NotAnId();
        }
        ProjectSnapshot snapshot = await service.OpenAsync(http.RequireUser(), project, cancellationToken);
        http.Response.Headers[RevisionHeader] = snapshot.Revision.ToString(CultureInfo.InvariantCulture);
        return Results.Text(ProjectJson.Write(snapshot.Document), "application/json");
    }

    private static async Task<IResult> SaveAsync(HttpContext http, string id, ProjectService service, CancellationToken cancellationToken)
    {
        return TryParse(id, out ProjectId project)
            ? Results.Ok(await service.SaveAsync(http.RequireUser(), project, cancellationToken))
            : NotAnId();
    }

    private static async Task<IResult> DeleteAsync(HttpContext http, string id, ProjectService service, CancellationToken cancellationToken)
    {
        if (!TryParse(id, out ProjectId project))
        {
            return NotAnId();
        }
        await service.DeleteAsync(http.RequireUser(), project, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DuplicateAsync(HttpContext http, string id, DuplicateProjectRequest? body, ProjectService service, CancellationToken cancellationToken)
    {
        if (!TryParse(id, out ProjectId project))
        {
            return NotAnId();
        }
        ProjectSummary copy = await service.DuplicateAsync(http.RequireUser(), project, body?.Name, cancellationToken);
        return Results.Created($"{http.Request.PathBase}/api/projects/{copy.Id}", copy);
    }

    private static bool TryParse(string raw, out ProjectId id)
    {
        id = default;
        if (!IdRules.IsValid(raw))
        {
            return false;
        }
        id = ProjectId.From(raw);
        return true;
    }

    private static IResult NotAnId() => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "That is not a project id.");
}
