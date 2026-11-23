using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Routes;

namespace AudioLab.Server.Render;

/// <summary>The export routes: parse, call <see cref="ExportService"/>, stream the answer. Behind the door, so the owner is its user.</summary>
public static class ExportRoutes
{
    /// <summary>Maps <c>/projects/{id}/export</c> into <paramref name="routes"/>, a group behind the door filter.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder export = routes.MapGroup("/projects/{id}/export")
            .AddEndpointFilter<StoreErrorFilter>()
            .AddEndpointFilter<ExportErrorFilter>();
        export.MapPost("/mix", MixAsync);
        export.MapPost("/stems", StemsAsync);
    }

    private static Task<IResult> MixAsync(
        HttpContext http,
        string id,
        ExportRequest? body,
        ExportService service,
        CancellationToken cancellationToken)
    {
        return StreamAsync(http, id, ExportKind.Mix, body, service, cancellationToken);
    }

    private static Task<IResult> StemsAsync(
        HttpContext http,
        string id,
        ExportRequest? body,
        ExportService service,
        CancellationToken cancellationToken)
    {
        return StreamAsync(http, id, ExportKind.Stems, body, service, cancellationToken);
    }

    // Everything that can refuse has run before the first byte; after it the filter can only cut the connection.
    private static async Task<IResult> StreamAsync(
        HttpContext http,
        string id,
        ExportKind kind,
        ExportRequest? body,
        ExportService service,
        CancellationToken cancellationToken)
    {
        if (!IdRules.IsValid(id))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "That is not a project id.");
        }
        using PreparedExport export = await service.PrepareAsync(http.RequireUser(), ProjectId.From(id), kind, body, cancellationToken);
        AttachmentResponse.Apply(http.Response, export.ContentType, export.FileName, export.Length);
        await export.WriteAsync(http.Response.Body);
        return Results.Empty;
    }

}
