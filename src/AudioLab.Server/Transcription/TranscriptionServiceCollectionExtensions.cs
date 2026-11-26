using AudioLab.Core.Transcription;
using AudioLab.Server.Media;

namespace AudioLab.Server.Transcription;

/// <summary>Adds the transcription jobs to the server's services (D96).</summary>
public static class TranscriptionServiceCollectionExtensions
{
    /// <summary>How long the host waits past one model timeout for the chunk in flight to be abandoned and the jobs paused (D93).</summary>
    public static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(30);

    /// <summary>Registers the store, the job table, the worker, the window source and the speech model registry (D98).</summary>
    public static IServiceCollection AddTranscription(this IServiceCollection services, TranscriptionOptions options)
    {
        return services.AddTranscription(options, SpeechModelCatalog.Entries);
    }

    /// <summary>Registers the transcription services for the given registry entries. The entries are checked here, once, so a bad one stops
    /// the start (D98).</summary>
    /// <exception cref="ArgumentException">An entry is not one the registry accepts.</exception>
    public static IServiceCollection AddTranscription(
        this IServiceCollection services,
        TranscriptionOptions options,
        IEnumerable<SpeechModelEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(entries);
        options.Validate();
        List<SpeechModelEntry> registry = [.. entries];
        SpeechModelCatalog.Validate(registry);
        services.AddSingleton(options);
        services.AddSingleton<TranscriptionStore>();
        services.AddSingleton<IAudioWindowSource, StoredAudioWindowSource>();
        // Only a model whose files are on disk is registered: a model that is not installed cannot be chosen (D98).
        foreach (SpeechModelEntry entry in registry.Where(entry => HartsyInferenceSpeechModel.IsInstalled(options, entry)))
        {
            services.AddSingleton<ISpeechModel>(provider => new HartsyInferenceSpeechModel(
                options,
                entry,
                provider.GetRequiredService<ILogger<HartsyInferenceSpeechModel>>()));
        }
        foreach (SpeechModelEntry entry in registry)
        {
            services.AddSingleton(entry);
        }
        services.AddSingleton<ISpeechModels>(provider => new SpeechModels(
            provider.GetServices<SpeechModelEntry>(),
            provider.GetServices<ISpeechModel>()));
        services.Configure<HostOptions>(host => host.ShutdownTimeout = options.ModelTimeout + StopGrace);
        services.AddSingleton<TranscriptionJobs>();
        services.AddSingleton<IMediaRemoval>(provider => provider.GetRequiredService<TranscriptionJobs>());
        services.AddSingleton<TranscriptionService>();
        services.AddHostedService<TranscriptionWorker>();
        return services;
    }
}
