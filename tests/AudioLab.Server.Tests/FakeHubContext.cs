using AudioLab.Server.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Server.Tests;

/// <summary>A hub context whose one connection is a <see cref="FakeProjectClient"/>.</summary>
public sealed class FakeHubContext(FakeProjectClient client) : IHubContext<ProjectHub, IProjectClient>
{
    public IHubClients<IProjectClient> Clients { get; } = new FakeHubClients(client);

    public IGroupManager Groups => throw new NotSupportedException();
}
