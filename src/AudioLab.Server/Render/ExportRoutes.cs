using System.Text.Json;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Routes;
using Microsoft.Net.Http.Headers;

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

    private static Task<IResult> MixAsync(HttpContext http, string id, ExportService service, CancellationToken cancellationToken)
    {
        return StreamAsync(http, id, ExportKind.Mix, service, cancellationToken);
    }

    private static Task<IResult> StemsAsync(HttpContext http, string id, ExportService service, CancellationToken cancellationToken)
    {
        return StreamAsync(http, id, ExportKind.Stems, service, cancellationToken);
    }

    private static bool HasBody(HttpContext http) => http.Request.ContentLength is not (null or 0) || !string.IsNullOrEmpty(http.Request.ContentType);

    // Read here and not bound by the framework, so that a body that is not an export request is refused as bad_options like any other
    // option that is wrong, in the same shape, and not as a bare 400.
    private static async Task<ExportRequest?> ReadRequestAsync(HttpContext http, CancellationToken cancellationToken)
    {
        if (!HasBody(http))
        {
            return null;
        }
        try
        {
            return await http.Request.ReadFromJsonAsync(ApiJsonContext.Default.ExportRequest, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ExportRefusedException(ExportRefusal.BadOptions, $"The body is not a valid export request: {exception.Message}");
        }
    }

    // Everything that can refuse has run before the first byte; after it the filter can only cut the connection.
    private static async Task<IResult> StreamAsync(
        HttpContext http,
        string id,
        ExportKind kind,
        ExportService service,
        CancellationToken cancellationToken)
    {
        if (!IdRules.IsValid(id))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "That is not a project id.");
        }
        if (HasBody(http) && !http.Request.HasJsonContentType())
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Send the options as JSON with the content type application/json.");
        }
        ExportRequest? body = await ReadRequestAsync(http, cancellationToken);
        using PreparedExport export = await service.PrepareAsync(http.RequireUser(), ProjectId.From(id), kind, body, cancellationToken);
        ContentDispositionHeaderValue disposition = new("attachment");
        disposition.SetHttpFileName(export.FileName);
        http.Response.ContentType = export.ContentType;
        http.Response.ContentLength = export.Length;
        http.Response.Headers.ContentDisposition = disposition.ToString();
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        await export.WriteAsync(http.Response.Body);
        return Results.Empty;
    }

}
