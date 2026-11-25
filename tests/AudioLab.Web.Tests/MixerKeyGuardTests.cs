using AudioLab.Core.Model;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The mixer keeps the keys of its sliders from the transport, once, for its own container.</summary>
public sealed class MixerKeyGuardTests
{
    private static ProjectDocument Mix()
    {
        Track kick = Song.NewTrack("kick") with { Name = "Kick" };
        return Song.Empty().With([kick]);
    }

    private static Dictionary<string, object?> MixerParameters(ProjectDocument document)
    {
        return new Dictionary<string, object?>
        {
            ["Document"] = document,
            ["Actions"] = new SessionActions { CanEdit = true },
            ["Selected"] = null,
        };
    }

    private static void MixerServices(IServiceCollection services, IKeyGuard guard)
    {
        services.AddSingleton(guard);
        services.AddSingleton(new TrackColorMap([.. Enumerable.Repeat("#ee6d86", 12)]));
        services.AddScoped<SessionSelection>();
        services.AddScoped<EffectSelection>();
        services.AddScoped<BottomPanelState>();
        services.AddScoped<EffectsNavigator>();
    }

    /// <summary>The first draw keeps the slider keys from the transport in the mixer's container, once.</summary>
    [Fact]
    public async Task TheMixerKeepsItsSliderKeysOnceForItsOwnContainer()
    {
        RecordingKeyGuard guard = new();
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix()), services => MixerServices(services, guard));

        Assert.Single(guard.Calls);
        Assert.Equal("slider", guard.Calls[0].Role);
        Assert.Contains(guard.Calls[0].Container, mixer.Values("id"));

        await mixer.RenderAgainAsync(MixerParameters(Mix()));

        Assert.Single(guard.Calls);
        await mixer.DisposeServicesAsync();
    }

    private sealed class RecordingKeyGuard : IKeyGuard
    {
        public List<(string Container, string Role)> Calls { get; } = [];

        public void PreventNavigationKeys(string containerId, string targetRole) => Calls.Add((containerId, targetRole));
    }
}
