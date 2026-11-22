using AudioLab.Server.Configuration;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Server.Realtime;

/// <summary>Makes the outbox of a new connection.</summary>
public sealed class ConnectionOutboxFactory(IHubContext<ProjectHub, IProjectClient> hub, AudioLabOptions options, ILogger<ConnectionOutbox> logger)
{
    /// <summary>The outbox for the connection <paramref name="context"/> belongs to; it aborts that connection if a send takes too long.</summary>
    public ConnectionOutbox Create(HubCallerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ConnectionOutbox(hub, context.ConnectionId, context.Abort, options.HubSendTimeout, logger);
    }
}
