using AudioLab.Core.Model;
using AudioLab.Server.Configuration;
using AudioLab.Server.Doors;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AudioLab.Server.Mcp;

/// <summary>Adds the MCP server to the host and maps its one endpoint, behind the door like every route but the health check.</summary>
public static class McpEndpoints
{
    /// <summary>The path of the endpoint inside the API group.</summary>
    public const string Path = "/mcp";

    /// <summary>Registers the MCP server and the services its tools run through.</summary>
    public static IServiceCollection AddAudioLabMcp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpContextAccessor();
        services.AddSingleton(provider => new McpCallLimiter(provider.GetRequiredService<AudioLabOptions>().Mcp.MaxConcurrentCallsPerUser));
        services.AddSingleton<McpClientIds>();
        services.AddSingleton<McpToolCatalog>();
        services.AddSingleton<McpToolService>();
        services.AddSingleton<McpResourceService>();
        services.AddMcpServer(server =>
        {
            server.ServerInfo = new Implementation { Name = "audiolab", Version = ServerVersion() };
            server.ServerInstructions = "AudioLab is a digital audio workstation. Tools act on the projects of the signed-in user. "
                + "Start with list_projects, then read a project with get_project. Every result is JSON.";
        })
            .WithHttpTransport(http => http.Stateless = true)
            .WithListToolsHandler((request, _) => ValueTask.FromResult(new ListToolsResult { Tools = [.. Catalog(request).Tools] }))
            .WithCallToolHandler(async (request, cancellationToken) =>
            {
                string name = request.Params?.Name ?? throw new McpProtocolException("A tool call needs a name.", McpErrorCode.InvalidParams);
                return await Service<McpToolService>(request).CallAsync(User(request), name, request.Params?.Arguments, cancellationToken);
            })
            .WithListResourcesHandler(
                async (request, cancellationToken) => await Service<McpResourceService>(request).ListAsync(User(request), cancellationToken))
            .WithListResourceTemplatesHandler((_, _) => ValueTask.FromResult(McpResourceService.Templates()))
            .WithReadResourceHandler(async (request, cancellationToken) =>
            {
                string uri = request.Params?.Uri ?? throw new McpProtocolException("A read needs a uri.", McpErrorCode.InvalidParams);
                return await Service<McpResourceService>(request).ReadAsync(User(request), uri, cancellationToken);
            });
        return services;
    }

    /// <summary>Maps the endpoint into <paramref name="api"/>, a group that carries <see cref="DoorRequired"/>, with its request checks.</summary>
    public static void Map(IEndpointRouteBuilder api, AudioLabOptions options)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(options);

        // Built here, not on the first call, so a tool that cannot be offered stops the server from starting.
        _ = api.ServiceProvider.GetRequiredService<McpToolCatalog>();
        McpCallLimiter limiter = api.ServiceProvider.GetRequiredService<McpCallLimiter>();
        api.MapMcp(Path).Add(endpoint => endpoint.RequestDelegate = McpRequestGuard.Wrap(endpoint.RequestDelegate!, options.Mcp, limiter));
    }

    private static McpToolCatalog Catalog<T>(RequestContext<T> request) => Service<McpToolCatalog>(request);

    private static TService Service<TService>(MessageContext request)
        where TService : notnull
    {
        return request.Services!.GetRequiredService<TService>();
    }

    private static UserId User(MessageContext request)
    {
        return request.Services!.GetRequiredService<IHttpContextAccessor>().HttpContext!.RequireUser();
    }

    private static string ServerVersion() => typeof(McpEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
