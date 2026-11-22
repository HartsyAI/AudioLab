using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

/// <summary>The real server on a loopback port the system picks, with its own store folder and a client that talks to it.</summary>
public sealed class RunningServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly TempFolder _folder;

    private RunningServer(WebApplication app, HttpClient client, TempFolder folder)
    {
        _app = app;
        Client = client;
        _folder = folder;
    }

    public HttpClient Client { get; }

    public string StoreFolder => _folder.Path;

    public IServiceProvider Services => _app.Services;

    public string HubPath => (Services.GetRequiredService<AudioLab.Server.Configuration.AudioLabOptions>().PathBase + "/api/hub");

    public static async Task<RunningServer> StartAsync(IDictionary<string, string?>? settings = null, Action<IServiceCollection>? configureServices = null)
    {
        TempFolder folder = new();
        Dictionary<string, string?> all = new()
        {
            ["urls"] = "http://127.0.0.1:0",
            ["Logging:LogLevel:Default"] = "Warning",
            ["AudioLab:Storage:Root"] = folder.Path,
        };
        foreach (KeyValuePair<string, string?> pair in settings ?? new Dictionary<string, string?>())
        {
            all[pair.Key] = pair.Value;
        }
        WebApplication app = ServerHost.Build([], all, configureServices);
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new RunningServer(app, new HttpClient { BaseAddress = new Uri(address) }, folder);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        _folder.Dispose();
    }
}
