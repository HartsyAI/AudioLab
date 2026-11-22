using AudioLab.Server.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Server.Tests;

/// <summary>The clients of a hub, of which the outbox uses only one connection's.</summary>
public sealed class FakeHubClients(FakeProjectClient client) : IHubClients<IProjectClient>
{
    public IProjectClient All => throw new NotSupportedException();

    public IProjectClient Client(string connectionId) => client;

    public IProjectClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

    public IProjectClient Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

    public IProjectClient Group(string groupName) => throw new NotSupportedException();

    public IProjectClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

    public IProjectClient Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

    public IProjectClient User(string userId) => throw new NotSupportedException();

    public IProjectClient Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
}
