using AudioLab.Core.Model;
using AudioLab.Server.Doors;
using AudioLab.Server.Media;

namespace AudioLab.Server.Transcription;

/// <summary>The transcription routes (D97): parse, call <see cref="TranscriptionService"/>, return. Behind the door, like the media routes.</summary>
public static class TranscriptionRoutes
{
    /// <summary>Maps the routes of one media file's transcription into <paramref name="routes"/>.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/media/{id}")
            .AddEndpointFilter<TranscriptionErrorFilter>()
            .AddEndpointFilter<MediaErrorFilter>();
        group.MapPost("/transcription", StartAsync);
        group.MapGet("/transcription", StatusAsync);
        group.MapPost("/transcription/cancel", CancelAsync);
        group.MapPost("/transcription/resume", ResumeAsync);
        group.MapGet("/transcript", TranscriptAsync);
    }

    private static async Task<IResult> StartAsync(HttpContext http, string id, TranscriptionService service, CancellationToken cancellationToken)
    {
        if (!TryParse(id, out MediaId media))
        {
            return NotAnId();
        }
        StartOutcome outcome = await service.StartAsync(http.RequireUser(), media, cancellationToken);
        return outcome.Created
            ? Results.Created($"{http.Request.PathBase}/api/media/{id}/transcription", outcome.Status)
            : Results.Ok(outcome.Status);
    }

    private static async Task<IResult> StatusAsync(HttpContext http, string id, TranscriptionService service, CancellationToken cancellationToken)
    {
        return TryParse(id, out MediaId media)
            ? Results.Ok(await service.StatusAsync(http.RequireUser(), media, cancellationToken))
            : NotAnId();
    }

    private static async Task<IResult> CancelAsync(HttpContext http, string id, TranscriptionService service, CancellationToken cancellationToken)
    {
        return TryParse(id, out MediaId media)
            ? Results.Accepted(null, await service.CancelAsync(http.RequireUser(), media, cancellationToken))
            : NotAnId();
    }

    private static async Task<IResult> ResumeAsync(HttpContext http, string id, TranscriptionService service, CancellationToken cancellationToken)
    {
        return TryParse(id, out MediaId media)
            ? Results.Accepted(null, await service.ResumeAsync(http.RequireUser(), media, cancellationToken))
            : NotAnId();
    }

    private static async Task<IResult> TranscriptAsync(HttpContext http, string id, TranscriptionService service, CancellationToken cancellationToken)
    {
        return TryParse(id, out MediaId media)
            ? Results.Ok(await service.TranscriptAsync(http.RequireUser(), media, cancellationToken))
            : NotAnId();
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
