using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>What a change to the master redraws, and that its fader and limiter are offered only when they can act.</summary>
public sealed class MasterRedrawTests
{
    private static ProjectDocument Mix(double masterVolume = 1, bool limiter = false)
    {
        Track kick = Song.NewTrack("kick") with { Name = "Kick" };
        Track bass = Song.NewTrack("bass") with { Name = "Bass" };
        return Song.Empty().With([kick, bass]) with { MasterVolume = masterVolume, MasterLimiter = limiter };
    }

    private static Dictionary<string, object?> MixerParameters(ProjectDocument document, bool canEdit = true)
    {
        return new Dictionary<string, object?>
        {
            ["Document"] = document,
            ["Actions"] = canEdit ? new SessionActions { CanEdit = true } : SessionActions.ReadOnly("Offline."),
            ["Selected"] = null,
        };
    }

    private static void MixerServices(IServiceCollection services)
    {
        services.AddSingleton(new TrackColorMap([.. Enumerable.Repeat("#ee6d86", 12)]));
        services.AddScoped<SessionSelection>();
        services.AddScoped<EffectSelection>();
        services.AddScoped<BottomPanelState>();
        services.AddScoped<EffectsNavigator>();
    }

    // Static text is written as markup and dynamic text as a text frame, so an assertion reads both.
    private static string Html(RenderedMarkup rendered) => string.Join(" ", rendered.Markup.Concat(rendered.Texts));

    /// <summary>A change to the master fader draws the master's strip alone; no track strip is drawn for it.</summary>
    [Fact]
    public async Task AMasterFaderChangeDrawsOnlyTheMasterStrip()
    {
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix()), MixerServices);

        await mixer.RenderAgainAsync(MixerParameters(Mix(masterVolume: 0.5)));

        Assert.Equal(1, mixer.Drawn.Count(type => type == typeof(ChannelStrip)));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>The master's limiter is always a named toggle, and when the screen cannot edit it is disabled with the screen's reason.</summary>
    [Fact]
    public async Task TheLimiterIsNamedAndSaysWhyItIsDisabled()
    {
        RenderedMarkup readOnly = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix(limiter: true), canEdit: false), MixerServices);

        Assert.Contains("Master limiter", readOnly.Values("aria-label"));
        Assert.Contains(readOnly.Values("title"), title => title.Contains("Offline.", StringComparison.Ordinal));
        await readOnly.DisposeServicesAsync();
    }
}
