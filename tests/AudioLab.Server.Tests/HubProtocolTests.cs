using System.Reflection;
using AudioLab.Core.Realtime;
using AudioLab.Server.Realtime;

namespace AudioLab.Server.Tests;

public sealed class HubProtocolTests
{
    [Fact]
    public void TheClientCallbacksCarryTheNamesTheProtocolPublishes()
    {
        string[] methods = [.. typeof(IProjectClient).GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal)];

        Assert.Equal([HubProtocol.DocumentChanged, HubProtocol.ProjectDeleted], methods);
    }

    [Fact]
    public void TheHubMethodsCarryTheNamesTheProtocolPublishes()
    {
        string[] names =
        [
            .. typeof(ProjectHub).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => method.GetCustomAttribute<Microsoft.AspNetCore.SignalR.HubMethodNameAttribute>()?.Name)
                .OfType<string>()
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(
            [HubProtocol.Apply, HubProtocol.EndGesture, HubProtocol.Join, HubProtocol.Leave, HubProtocol.Redo, HubProtocol.Undo],
            names);
    }
}
