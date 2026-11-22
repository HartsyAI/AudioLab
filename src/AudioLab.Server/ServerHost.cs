using System.Text.Json;
using AudioLab.Server.Configuration;
using AudioLab.Server.Doors;
using AudioLab.Server.Json;
using AudioLab.Server.Routes;
using AudioLab.Server.Services;
using Microsoft.Extensions.Options;

namespace AudioLab.Server;

/// <summary>Builds the server: configuration, door, services and routes. Tests and the desktop shell call it as Program does.</summary>
public static class ServerHost
{
    /// <summary>Builds the host from command-line arguments and configuration, refusing a configuration it must not run with.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="settings">Configuration values that win over every other source; used by tests and by a shell that starts the server in-process.</param>
    /// <exception cref="InvalidOperationException">The configuration is not one the server may start with; the message lists every problem.</exception>
    public static WebApplication Build(string[] args, IReadOnlyDictionary<string, string?>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        if (settings is not null)
        {
            builder.Configuration.AddInMemoryCollection(settings);
        }
        AudioLabOptions options = ReadOptions(builder.Configuration);
        ConfigureLogging(builder.Logging);
        ConfigureServices(builder.Services, options);
        WebApplication app = builder.Build();
        MapPipeline(app, options);
        return app;
    }

    private static AudioLabOptions ReadOptions(IConfiguration configuration)
    {
        AudioLabOptions options = configuration.GetSection(AudioLabOptions.Section).Get<AudioLabOptions>() ?? new AudioLabOptions();
        List<string> problems = [];
        ValidateOptionsResult result = new AudioLabOptionsValidator().Validate(null, options);
        if (result.Failures is not null)
        {
            problems.AddRange(result.Failures);
        }
        if (Enum.IsDefined(options.Door.Kind))
        {
            problems.AddRange(ListenGuard.Problems(options.Door.Kind, configuration));
        }
        if (problems.Count > 0)
        {
            throw new InvalidOperationException($"The server cannot start with this configuration: {string.Join(" ", problems)}");
        }
        return options;
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole(console =>
        {
            console.UseUtcTimestamp = true;
            console.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            console.IncludeScopes = true;
            console.JsonWriterOptions = new JsonWriterOptions { Indented = false };
        });
    }

    private static void ConfigureServices(IServiceCollection services, AudioLabOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IDoor>(DoorFactory.Create(options.Door));
        services.AddSingleton<HealthService>();
        services.AddSingleton<SessionInfoService>();
        services.AddProblemDetails();
        services.ConfigureHttpJsonOptions(json => json.SerializerOptions.TypeInfoResolverChain.Insert(0, ServerJsonContext.Default));
    }

    private static void MapPipeline(WebApplication app, AudioLabOptions options)
    {
        if (options.PathBase.Length > 0)
        {
            app.UsePathBase(options.PathBase);
        }
        app.UseExceptionHandler();
        app.UseRouting();
        HealthRoutes.Map(app);
        RouteGroupBuilder api = app.MapGroup("/api").AddEndpointFilter<DoorFilter>();
        SessionRoutes.Map(api);
    }
}
