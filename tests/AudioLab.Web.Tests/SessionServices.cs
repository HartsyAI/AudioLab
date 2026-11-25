using AudioLab.Core.Commands;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Media;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using AudioLab.Web.Session.Roll;
using AudioLab.Web.Waveforms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AudioLab.Web.Tests;

/// <summary>The services a project page needs, registered as the composition root registers them, with the browser's parts answered by fakes.</summary>
/// <remarks>Registered with TryAdd, so a test that needs one of them registers its own before calling <see cref="Add"/>.</remarks>
internal static class SessionServices
{
    /// <summary>Registers the page's services; the browser-facing ones do nothing, and the rest are the app's own classes.</summary>
    /// <param name="services">The collection the render uses.</param>
    /// <param name="http">The client the services call the server with.</param>
    public static void Add(IServiceCollection services, HttpClient http)
    {
        services.TryAddSingleton(http);
        services.TryAddSingleton(new CommandSender(CoreCommands.CreateRegistry()));
        services.TryAddSingleton<INativeOverlay, NoOverlay>();
        services.TryAddSingleton<IElementClick, NoClick>();
        services.TryAddSingleton<IElementMetrics, NoMetrics>();
        services.TryAddSingleton<ITokenReader, FixedTokens>();
        services.TryAddSingleton<IShortcutListener, NoShortcuts>();
        services.TryAddSingleton<IAuditionSink, UnavailableAudition>();
        services.TryAddSingleton<IMidiTransfer, UnavailableMidiTransfer>();
        services.TryAddSingleton<ILyricsModels, UnavailableLyricsModels>();
        services.TryAddSingleton<LiveProjectFactory>();
        services.TryAddSingleton<SessionClient>();
        services.TryAddSingleton<ProjectApi>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new ToastService(TimeProvider.System));
        services.TryAddScoped(provider => new MediaApi(
            provider.GetRequiredService<HttpClient>(),
            new HttpClient { BaseAddress = http.BaseAddress, Timeout = Timeout.InfiniteTimeSpan }));
        services.TryAddSingleton(provider => new TrackColorMap(
            [.. Enumerable.Range(1, 12).Select(number => provider.GetRequiredService<ITokenReader>().Read($"--track-{number}"))]));
        services.TryAddScoped<AutomationLanes>();
        services.TryAddScoped<AutomationRecorder>();
        services.TryAddScoped<TimelineZoom>();
        services.TryAddScoped<MediaLibrary>();
        services.TryAddScoped<MediaDragState>();
        services.TryAddScoped<MediaAdder>();
        services.TryAddScoped<MediaDragger>();
        services.TryAddScoped<ProjectRenamer>();
        services.TryAddScoped<SessionSelection>();
        services.TryAddScoped<ClipDragState>();
        services.TryAddScoped<BottomPanelState>();
        services.TryAddScoped<LyricsCursor>();
        services.TryAddScoped<LyricsDrags>();
        services.TryAddScoped<EffectSelection>();
        services.TryAddScoped<EffectsNavigator>();
        services.TryAddScoped<PianoRollState>();
        services.TryAddScoped<RollDragState>();
        services.TryAddScoped<PianoRollKeys>();
        services.TryAddScoped<NoteAudition>();
        services.TryAddScoped<PeakStore>();
        services.TryAddScoped<WaveformService>();
        NoElementServices.Add(services);
    }

    private sealed class NoOverlay : INativeOverlay
    {
        public void ShowModal(string dialogId)
        {
        }

        public void OnClosed(string dialogId, Action closed)
        {
        }

        public void KeepOpenWhileInnerOverlayIsExpanded(string dialogId)
        {
        }

        public void Close(string dialogId)
        {
        }

        public void KeepAboveModals(string popoverId)
        {
        }
    }

    private sealed class NoClick : IElementClick
    {
        public void Click(string elementId)
        {
        }
    }

    private sealed class NoMetrics : IElementMetrics
    {
        public (double Left, double Top)? Origin(string elementId) => null;

        public bool IsOver(string elementId, double clientX, double clientY) => false;
    }

    private sealed class FixedTokens : ITokenReader
    {
        public string Read(string token) => "#808080";
    }

    private sealed class NoShortcuts : IShortcutListener
    {
        public void Listen(Func<KeyChord, bool> handler)
        {
        }

        public void Stop()
        {
        }
    }
}
