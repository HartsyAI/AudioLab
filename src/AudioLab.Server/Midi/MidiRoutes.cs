using System.Globalization;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Routes;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace AudioLab.Server.Midi;

/// <summary>The MIDI routes: parse, call <see cref="MidiService"/>, return. Behind the door, so the owner is the session's user.</summary>
public static class MidiRoutes
{
    private const string MidiType = "audio/midi";

    private static readonly HashSet<string> _midiTypes = new(StringComparer.OrdinalIgnoreCase) { MidiType, "audio/x-midi", "audio/mid" };

    /// <summary>Maps the MIDI routes of <c>/projects/{id}</c> into <paramref name="routes"/>, a group behind the door filter.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder midi = routes.MapGroup("/projects/{id}")
            .AddEndpointFilter<StoreErrorFilter>()
            .AddEndpointFilter<MidiErrorFilter>();
        midi.MapPost("/tracks/{track}/midi", ImportAsync);
        midi.MapGet("/tracks/{track}/midi", ExportTrackAsync);
        midi.MapGet("/clips/{clip}/midi", ExportClipAsync);
    }

    // The body is the .mid file itself, not a form: it is read into memory under the limit and never as JSON.
    private static async Task<IResult> ImportAsync(
        HttpContext http,
        string id,
        string track,
        MidiService service,
        MidiOptions options,
        CancellationToken cancellationToken)
    {
        // Before any early refusal: a body the server does not read is drained under this limit, and a lower one resets the client.
        IHttpMaxRequestBodySizeFeature? limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = options.MaxFileBytes;
        }
        if (!MediaTypeHeaderValue.TryParse(http.Request.ContentType, out MediaTypeHeaderValue? type)
            || !_midiTypes.Contains(type.MediaType.Value ?? string.Empty))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Send the .mid file as the body with the content type audio/midi.");
        }
        if (!IdRules.IsValid(id) || !IdRules.IsValid(track))
        {
            return NotAnId();
        }
        IResult? repeated = OnlyOnce(http, "start", out string? startText);
        string? name = null;
        if (repeated is null)
        {
            repeated = OnlyOnce(http, "name", out name);
        }
        if (repeated is not null)
        {
            return repeated;
        }
        double start = 0;
        if (startText is not null
            && (!double.TryParse(startText, NumberStyles.Float, CultureInfo.InvariantCulture, out start) || !double.IsFinite(start)))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "start is a number of seconds.");
        }
        MidiImportResult result = await service.ImportAsync(
            http.RequireUser(),
            ProjectId.From(id),
            TrackId.From(track),
            start,
            name,
            http.Request.Body,
            cancellationToken);
        return Results.Created($"{http.Request.PathBase}/api/projects/{id}/clips/{result.Clip}/midi", result);
    }

    private static async Task<IResult> ExportTrackAsync(
        HttpContext http,
        string id,
        string track,
        MidiService service,
        CancellationToken cancellationToken)
    {
        if (!IdRules.IsValid(id) || !IdRules.IsValid(track))
        {
            return NotAnId();
        }
        MidiExport file = await service.ExportTrackAsync(http.RequireUser(), ProjectId.From(id), TrackId.From(track), cancellationToken);
        return Send(http, file);
    }

    private static async Task<IResult> ExportClipAsync(
        HttpContext http,
        string id,
        string clip,
        MidiService service,
        CancellationToken cancellationToken)
    {
        if (!IdRules.IsValid(id) || !IdRules.IsValid(clip))
        {
            return NotAnId();
        }
        MidiExport file = await service.ExportClipAsync(http.RequireUser(), ProjectId.From(id), ClipId.From(clip), cancellationToken);
        return Send(http, file);
    }

    private static IResult Send(HttpContext http, MidiExport file)
    {
        AttachmentResponse.Apply(http.Response, MidiType, file.FileName, file.Bytes.Length);
        return Results.Bytes(file.Bytes, MidiType);
    }

    // A query value that is there at most once; a refusal when it is repeated.
    private static IResult? OnlyOnce(HttpContext http, string key, out string? value)
    {
        value = null;
        if (!http.Request.Query.TryGetValue(key, out StringValues values))
        {
            return null;
        }
        if (values.Count != 1)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Send {key} at most once.");
        }
        value = values[0];
        return null;
    }

    private static IResult NotAnId() => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "That is not an id.");
}
