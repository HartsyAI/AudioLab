using AudioLab.Core.Transcription;
using AudioLab.Server.Media;

namespace AudioLab.Server.Transcription;

/// <summary>Adds the transcription jobs to the server's services (D96).</summary>
public static class TranscriptionServiceCollectionExtensions
{
    /// <summary>Registers the store, the job table, the worker, the window source and the speech model seam.</summary>
    public static IServiceCollection AddTranscription(this IServiceCollection services, TranscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        services.AddSingleton(options);
        services.AddSingleton<TranscriptionStore>();
        services.AddSingleton<IAudioWindowSource, StoredAudioWindowSource>();
        services.AddSingleton<ISpeechModel, UnavailableSpeechModel>();
        services.AddSingleton<TranscriptionJobs>();
        services.AddSingleton<IMediaRemoval>(provider => provider.GetRequiredService<TranscriptionJobs>());
        services.AddSingleton<TranscriptionService>();
        services.AddHostedService<TranscriptionWorker>();
        return services;
    }
}
