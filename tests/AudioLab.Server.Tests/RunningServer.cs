using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

/// <summary>The real server on a loopback port the system picks, with a client that talks to it.</summary>
public sealed class RunningServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private RunningServer(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<RunningServer> StartAsync(IDictionary<string, string?>? settings = null)
    {
        Dictionary<string, string?> all = new() { ["urls"] = "http://127.0.0.1:0", ["Logging:LogLevel:Default"] = "Warning" };
        foreach (KeyValuePair<string, string?> pair in settings ?? new Dictionary<string, string?>())
        {
            all[pair.Key] = pair.Value;
        }
        WebApplication app = ServerHost.Build([], all);
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new RunningServer(app, new HttpClient { BaseAddress = new Uri(address) });
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
