using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>Adds the media store to the server's services.</summary>
public static class MediaServiceCollectionExtensions
{
    /// <summary>Registers the media store, its quota, the service the routes call, and the catalog the command pipeline asks.</summary>
    public static IServiceCollection AddMedia(this IServiceCollection services, MediaOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton<IMediaQuota, OptionsMediaQuota>();
        services.AddSingleton<MediaStore>();
        services.AddSingleton<IMediaCatalog>(provider => provider.GetRequiredService<MediaStore>());
        services.AddSingleton<MediaReferences>();
        services.AddSingleton<PeakRebuilder>();
        services.AddSingleton<MediaService>();
        services.AddHostedService<MediaStartup>();
        return services;
    }
}
