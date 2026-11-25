using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>What a change to the master redraws, that its limiter says why it cannot be changed, and what a master drag sends.</summary>
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
        services.AddSingleton<IKeyGuard>(new NoKeyGuard());
        services.AddSingleton(new TrackColorMap([.. Enumerable.Repeat("#ee6d86", 12)]));
        services.AddScoped<SessionSelection>();
        services.AddScoped<EffectSelection>();
        services.AddScoped<BottomPanelState>();
        services.AddScoped<EffectsNavigator>();
    }

    /// <summary>A change to the master fader draws the master's strip alone; the one strip drawn is the master's.</summary>
    [Fact]
    public async Task AMasterFaderChangeDrawsOnlyTheMasterStrip()
    {
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix()), MixerServices);

        int textsBefore = mixer.Texts.Count;
        int attributesBefore = mixer.Attributes.Count;
        await mixer.RenderAgainAsync(MixerParameters(Mix(masterVolume: 0.5)));

        Assert.Equal(1, mixer.Drawn.Count(type => type == typeof(ChannelStrip)));
        // What the redraw wrote is the master's: its name, its fader and its readout, and no track's.
        Assert.Contains("-6.0", mixer.Texts.Skip(textsBefore));
        IEnumerable<string> written = mixer.Attributes.Skip(attributesBefore).Select(attribute => attribute.Value);
        Assert.Contains(written, value => value.StartsWith("Volume of Master:", StringComparison.Ordinal));
        Assert.DoesNotContain(written, value => value.StartsWith("Volume of Kick", StringComparison.Ordinal));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>The master's limiter is a named toggle, and when the screen cannot edit its own title says why.</summary>
    [Fact]
    public async Task TheLimiterIsNamedAndItsOwnTitleSaysWhyItIsDisabled()
    {
        RenderedMarkup readOnly = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix(limiter: true), canEdit: false), MixerServices);

        Assert.True(readOnly.TitleOfElementNamed("Master limiter", "Offline."));
        await readOnly.DisposeServicesAsync();
    }

    /// <summary>A drag on the master fader is drawn by the fader alone, sends nothing before the release, and sends one gain on it.</summary>
    [Fact]
    public async Task AMasterFaderDragSendsOneGainOnRelease()
    {
        DrivenPointer pointer = new();
        List<double> sent = [];
        Dictionary<string, object?> parameters = new()
        {
            ["Name"] = "Master",
            ["ColorToken"] = "var(--text)",
            ["Effects"] = (IReadOnlyList<StripEffect>)[],
            ["Volume"] = 1.0,
            ["Master"] = true,
            ["Editable"] = true,
            ["OnVolume"] = EventCallback.Factory.Create<double>(new object(), gain => sent.Add(gain)),
        };
        RenderedMarkup strip = await RenderedMarkup.StartAsync<ChannelStrip>(parameters, services => services.AddSingleton<IPointerTracker>(pointer));

        await strip.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        List<int> drawnPerMove = [];
        for (int step = 1; step <= 40; step++)
        {
            await strip.RunAsync(() => pointer.Move(0, 300 - (step * 2)));
            await Task.Yield();
            drawnPerMove.Add(strip.Drawn.Count);
        }

        Assert.All(drawnPerMove, count => Assert.Equal(1, count));
        Assert.All(strip.Drawn, type => Assert.Equal(typeof(Fader), type));
        Assert.Empty(sent);
        await strip.RunAsync(() => pointer.Release(0, 220));
        await UntilAsync(() => sent.Count == 1);
        Assert.Single(sent);
        await strip.DisposeServicesAsync();
    }

    /// <summary>A master fader that cannot be changed says why beside it, as well as in its title.</summary>
    [Fact]
    public async Task TheMasterFaderSaysWhyBesideItWhenItCannotBeChanged()
    {
        RenderedMarkup strip = await RenderedMarkup.StartAsync<ChannelStrip>(new Dictionary<string, object?>
        {
            ["Name"] = "Master",
            ["ColorToken"] = "var(--text)",
            ["Effects"] = (IReadOnlyList<StripEffect>)[],
            ["Volume"] = 1.0,
            ["Master"] = true,
            ["Editable"] = false,
            ["Reason"] = "Offline.",
            ["OnVolume"] = EventCallback.Factory.Create<double>(new object(), _ => { }),
        });

        Assert.Contains("Offline.", strip.Texts);
        await strip.DisposeServicesAsync();
    }

    // Waits for a condition the handler reaches on another task; fails rather than waits for ever.
    private static async Task UntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(5);
        }
    }

    private sealed class DrivenPointer : IPointerTracker
    {
        private Action<double, double>? _move;

        private Action<double, double>? _end;

        public bool Shift => false;

        public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
        {
            (_move, _end) = (move, end);
        }

        public void Stop()
        {
            (_move, _end) = (null, null);
        }

        public void Move(double x, double y) => _move?.Invoke(x, y);

        public void Release(double x, double y) => _end?.Invoke(x, y);
    }
}
