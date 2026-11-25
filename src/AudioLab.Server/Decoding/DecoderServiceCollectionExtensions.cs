using AudioLab.Server.Services;

namespace AudioLab.Server.Decoding;

/// <summary>Adds the optional audio decoder to the server's services.</summary>
public static class DecoderServiceCollectionExtensions
{
    /// <summary>Registers the decoder, its installer for this platform's pinned build, and the gate that limits decodes.</summary>
    public static IServiceCollection AddDecoder(this IServiceCollection services, DecoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton<DecoderLocation>();
        services.AddSingleton<IDecoderDownloader, HttpDecoderDownloader>();
        services.AddSingleton<IDecoderLauncher, SystemDecoderLauncher>();
        services.AddSingleton<DecoderInstaller>(provider => new DecoderInstaller(
            options,
            provider.GetRequiredService<DecoderLocation>(),
            provider.GetRequiredService<IDecoderDownloader>(),
            DecoderPin.ForThisPlatform(),
            provider.GetRequiredService<ILogger<DecoderInstaller>>()));
        services.AddSingleton<FfmpegDecoder>();
        services.AddKeyedSingleton(
            DecodedUploadService.GateKey,
            (_, _) => new ConcurrencyGate(options.MaxConcurrentPerUser, options.MaxConcurrent, "decodes"));
        services.AddSingleton<DecodedUploadService>();
        return services;
    }
}
