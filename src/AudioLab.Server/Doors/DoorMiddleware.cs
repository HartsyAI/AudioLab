namespace AudioLab.Server.Doors;

/// <summary>Runs the door before any endpoint marked <see cref="DoorRequired"/>, so a hub's negotiate and its WebSocket upgrade get the same checks as a route.</summary>
public sealed class DoorMiddleware(RequestDelegate next, IDoor door, ILogger<DoorMiddleware> logger)
{
    /// <summary>Admits the request or answers with the door's refusal.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<DoorRequired>() is not null)
        {
            IResult? refusal = DoorGate.Admit(door, logger, context);
            if (refusal is not null)
            {
                await refusal.ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
