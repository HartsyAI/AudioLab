namespace AudioLab.Server.Doors;

/// <summary>Resolves the user of every request in a route group, or answers with the door's refusal.</summary>
public sealed class DoorFilter(IDoor door, ILogger<DoorFilter> logger) : IEndpointFilter
{
    /// <inheritdoc />
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        IResult? refusal = DoorGate.Admit(door, logger, context.HttpContext);
        return refusal is null ? next(context) : ValueTask.FromResult<object?>(refusal);
    }
}
