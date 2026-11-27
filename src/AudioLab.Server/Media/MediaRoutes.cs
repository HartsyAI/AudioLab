using AudioLab.Core.Model;
using AudioLab.Server.Decoding;
using AudioLab.Server.Doors;
using AudioLab.Server.Services;
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

    // Accepted only as a body the optional decoder turns into WAV; the decoder reads what it reads, and the WAV is checked as any upload.
    private static readonly HashSet<string> _compressedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg",
        "audio/mp3",
        "audio/mp4",
        "audio/m4a",
        "audio/x-m4a",
        "audio/flac",
        "audio/x-flac",
        "audio/ogg",
        "application/ogg",
    };

    /// <summary>Maps /media into <paramref name="routes"/>, a group behind the door filter.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder media = routes.MapGroup("/media").AddEndpointFilter<MediaErrorFilter>();
        media.MapGet("/", List);
        media.MapGet("/limits", LimitsAsync);
        media.MapPost("/", UploadAsync);
        media.MapGet("/{id}", Get);
        media.MapMethods("/{id}/content", ["GET", "HEAD"], Content);
        media.MapMethods("/{id}/peaks", ["GET", "HEAD"], PeaksAsync);
        media.MapDelete("/{id}", DeleteAsync);
    }

    private static IResult List(HttpContext http, MediaService service) => Results.Ok(service.List(http.RequireUser()));

    private static async Task<IResult> LimitsAsync(HttpContext http, MediaService service, CancellationToken cancellationToken)
    {
        return Results.Ok(await service.LimitsAsync(http.RequireUser(), cancellationToken));
    }

    // The body is the WAV file itself, not a form: it is streamed to disk, never held in memory.
    private static async Task<IResult> UploadAsync(
        HttpContext http,
        MediaService service,
        DecodedUploadService decoded,
        MediaOptions options,
        CancellationToken cancellationToken)
    {
        // Before any early refusal: a body the server does not read is drained under this limit, and a lower one resets the client.
        IHttpMaxRequestBodySizeFeature? limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = options.MaxUploadBytes;
        }
        string mediaType = MediaTypeHeaderValue.TryParse(http.Request.ContentType, out MediaTypeHeaderValue? type)
            ? type.MediaType.Value ?? string.Empty
            : string.Empty;
        bool wav = _wavTypes.Contains(mediaType);
        if (!wav && !_compressedTypes.Contains(mediaType))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Send the audio file as the body: a WAV file, or MP3, M4A, FLAC or OGG with its own content type.");
        }
        if (!http.Request.Query.TryGetValue("name", out StringValues names) || names.Count != 1)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Send the file's name once, as the query value name.");
        }
        UserId user = http.RequireUser();
        if (wav)
        {
            MediaRecord stored = await service.UploadAsync(user, names[0], http.Request.Body, http.Request.ContentLength, cancellationToken);
            return Results.Created($"{http.Request.PathBase}/api/media/{stored.Id.Value}", MediaSummary.Of(stored));
        }
        // A compressed file is decoded to WAV first, and then stored exactly like a WAV upload; without the decoder it is refused with the reason.
        try
        {
            MediaRecord converted = await decoded.UploadAsync(
                user,
                mediaType,
                names[0],
                http.Request.Body,
                http.Request.ContentLength,
                cancellationToken);
            return Results.Created($"{http.Request.PathBase}/api/media/{converted.Id.Value}", MediaSummary.Of(converted));
        }
        catch (DecoderNotInstalledException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
        catch (GateBusyException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: exception.Message);
        }
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

    // Peaks are made once, when the file is uploaded, so they are as unchanging as the bytes; a missing or damaged file is rebuilt.
    private static async Task<IResult> PeaksAsync(HttpContext http, string id, MediaService service, CancellationToken cancellationToken)
    {
        if (!TryParse(id, out MediaId media))
        {
            return NotAnId();
        }
        UserId user = http.RequireUser();
        MediaRecord record = service.Get(user, media);
        FileStream stream = await service.OpenPeaksAsync(user, media, cancellationToken);
        http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        EntityTagHeaderValue tag = new($"\"{record.Sha256}-peaks{PeakFile.Version}\"");
        return Results.File(stream, PeakFile.MediaType, lastModified: record.Created, entityTag: tag, enableRangeProcessing: true);
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
