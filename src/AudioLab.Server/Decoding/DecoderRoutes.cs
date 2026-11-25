using AudioLab.Core.Api.Decoder;

namespace AudioLab.Server.Decoding;

/// <summary>The decoder routes: the status, and the install that the user confirms. Behind the door; the decoder is one per server.</summary>
public static class DecoderRoutes
{
    /// <summary>Maps /decoder into <paramref name="routes"/>, a group behind the door filter.</summary>
    public static void Map(IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder decoder = routes.MapGroup("/decoder");
        decoder.MapGet("/", Status);
        decoder.MapPost("/install", Install);
    }

    private static IResult Status(DecoderInstaller installer) => Results.Ok(installer.Status());

    // The body names the version and checksum the user read on the confirmation; only that build is ever downloaded.
    private static IResult Install(DecoderInstallRequest request, DecoderInstaller installer)
    {
        return installer.Start(request) switch
        {
            DecoderInstallOutcome.Started => Results.Accepted("/api/decoder", installer.Status()),
            DecoderInstallOutcome.AlreadyInstalled => Results.Ok(installer.Status()),
            DecoderInstallOutcome.Busy => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "An install is already running; wait for it to finish."),
            DecoderInstallOutcome.Disabled => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Installing the decoder is turned off on this server."),
            DecoderInstallOutcome.Unsupported => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "No audio decoder is available for this platform."),
            _ => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The version or checksum does not match the build shown. Reload the page and confirm again."),
        };
    }
}
