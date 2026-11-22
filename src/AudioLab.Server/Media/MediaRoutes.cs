using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace AudioLab.Server.Media;

/// <summary>The media routes: parse, call <see cref="MediaService"/>, return. Behind the door, so the owner is always the session's user.</summary>
public static class MediaRoutes
{
    private const string WavType = "audio/wav";

    private static readonly HashSet<string> _wavTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        WavType,
        "audio/x-wav",
        "audio/wave",
        "audio/vnd.wave",
    };

    /// <summary>Maps /media into <paramref name="routes"/>, a group behind the door filter.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder media = routes.MapGroup("/media").AddEndpointFilter<MediaErrorFilter>();
        media.MapGet("/", List);
        media.MapPost("/", UploadAsync);
        media.MapGet("/{id}", Get);
        media.MapMethods("/{id}/content", ["GET", "HEAD"], Content);
        media.MapDelete("/{id}", DeleteAsync);
    }

    private static IResult List(HttpContext http, MediaService service) => Results.Ok(service.List(http.RequireUser()));

    // The body is the WAV file itself, not a form: it is streamed to disk, never held in memory.
    private static async Task<IResult> UploadAsync(HttpContext http, MediaService service, MediaOptions options, CancellationToken cancellationToken)
    {
        // Before any early refusal: a body the server does not read is drained under this limit, and a lower one resets the client.
        IHttpMaxRequestBodySizeFeature? limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = options.MaxUploadBytes;
        }
        if (!MediaTypeHeaderValue.TryParse(http.Request.ContentType, out MediaTypeHeaderValue? type)
            || !_wavTypes.Contains(type.MediaType.Value ?? string.Empty))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Send the WAV file as the body with the content type audio/wav.");
        }
        if (!http.Request.Query.TryGetValue("name", out StringValues names) || names.Count != 1)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Send the file's name once, as the query value name.");
        }
        UserId user = http.RequireUser();
        MediaRecord record = await service.UploadAsync(user, names[0], http.Request.Body, http.Request.ContentLength, cancellationToken);
        return Results.Created($"{http.Request.PathBase}/api/media/{record.Id.Value}", MediaSummary.Of(record));
    }

    private static IResult Get(HttpContext http, string id, MediaService service)
    {
        return TryParse(id, out MediaId media) ? Results.Ok(MediaSummary.Of(service.Get(http.RequireUser(), media))) : NotAnId();
    }

    // The bytes of a media file never change, so a client may keep them; the hash is the entity tag and range requests are answered by the framework.
    private static IResult Content(HttpContext http, string id, MediaService service)
    {
        if (!TryParse(id, out MediaId media))
        {
            return NotAnId();
        }
        UserId user = http.RequireUser();
        MediaRecord record = service.Get(user, media);
        FileStream stream = service.OpenContent(user, media);
        http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        EntityTagHeaderValue tag = new($"\"{record.Sha256}\"");
        return Results.File(stream, WavType, lastModified: record.Created, entityTag: tag, enableRangeProcessing: true);
    }

    private static async Task<IResult> DeleteAsync(HttpContext http, string id, MediaService service, CancellationToken cancellationToken)
    {
        if (!TryParse(id, out MediaId media))
        {
            return NotAnId();
        }
        await service.DeleteAsync(http.RequireUser(), media, cancellationToken);
        return Results.NoContent();
    }

    private static bool TryParse(string raw, out MediaId id)
    {
        id = default;
        if (!IdRules.IsValid(raw))
        {
            return false;
        }
        id = MediaId.From(raw);
        return true;
    }

    private static IResult NotAnId() => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "That is not a media id.");
}
