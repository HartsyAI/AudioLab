using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>Folding a group, the captions that say what is silenced and why, and what a change to a group redraws.</summary>
public sealed class GroupRedrawTests
{
    private static readonly GroupId _drums = GroupId.From("grp_drums");

    private static TrackGroup Drums(bool collapsed = false, bool muted = false, double volume = 1)
    {
        return new TrackGroup
        {
            Id = _drums,
            Name = "Drums",
            Color = "#ee6d86",
            Collapsed = collapsed,
            Tint = true,
            Muted = muted,
            Soloed = false,
            Volume = volume,
        };
    }

    // Two tracks in the group, then one loose track: the group's strip comes before its first member.
    private static ProjectDocument Mix(bool collapsed = false, bool muted = false, double volume = 1)
    {
        Track kick = Song.NewTrack("kick") with { Name = "Kick", Group = _drums };
        Track snare = Song.NewTrack("snare") with { Name = "Snare", Group = _drums };
        Track bass = Song.NewTrack("bass") with { Name = "Bass" };
        return Song.Empty().With([kick, snare, bass]) with { Groups = [Drums(collapsed, muted, volume)] };
    }

    private static Dictionary<string, object?> MixerParameters(ProjectDocument document, bool canEdit = false)
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

    // Static text is written as markup and dynamic text as a text frame, so an assertion reads both.
    private static string Html(RenderedMarkup rendered) => string.Join(" ", rendered.Markup.Concat(rendered.Texts));

    /// <summary>A folded group draws its strip alone, and its tracks are not drawn at all.</summary>
    [Fact]
    public async Task AFoldedGroupDrawsItsStripAndNotItsTracks()
    {
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix(collapsed: true)), MixerServices);

        Assert.Equal(1, mixer.Drawn.Count(type => type == typeof(GroupStrip)));
        // The loose track and the master: the two members are folded away.
        Assert.Equal(2, mixer.Drawn.Count(type => type == typeof(ChannelStrip)));
        Assert.Contains("Drums", Html(mixer));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>A muted group says in words why its members are silent, and a muted track says it is muted.</summary>
    [Fact]
    public async Task ASilencedTrackSaysWhy()
    {
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix(muted: true)), MixerServices);

        Assert.Contains("Muted by Drums", Html(mixer));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>A track heard only through a send into a soloed bus says so, rather than saying it is silenced.</summary>
    [Fact]
    public async Task ATrackHeardThroughASoloedBusSaysSo()
    {
        Track source = Song.NewTrack("source") with
        {
            Name = "Source",
            Sends = [new Send(TrackId.From("reverb"), 1, false, false)],
        };
        Track reverb = Song.NewTrack("reverb") with { Name = "Reverb", Kind = TrackKind.Bus, Soloed = true };
        Track other = Song.NewTrack("other") with { Name = "Other", Soloed = false };
        ProjectDocument document = Song.Empty().With([source, reverb, other]);

        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(document), MixerServices);

        // The source is heard; the unrelated track, which sends nowhere, is silenced by the solo.
        Assert.Contains("Heard through a soloed bus", Html(mixer));
        Assert.Contains("Silenced by a solo elsewhere", Html(mixer));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>A change to a group's volume draws its strip and no track strip: the volume is not in what a track shows.</summary>
    [Fact]
    public async Task AGroupVolumeDrawsOnlyTheGroupStrip()
    {
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix()), MixerServices);

        await mixer.RenderAgainAsync(MixerParameters(Mix(volume: 0.5)));

        Assert.Equal(1, mixer.Drawn.Count(type => type == typeof(GroupStrip)));
        Assert.Equal(0, mixer.Drawn.Count(type => type == typeof(ChannelStrip)));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>A colour that cannot be chosen is a disabled swatch, and the screen's reason is written beside it.</summary>
    [Fact]
    public async Task AColourThatCannotBeChosenSaysWhy()
    {
        RenderedMarkup strip = await RenderedMarkup.OfAsync<GroupStrip>(new Dictionary<string, object?>
        {
            ["Group"] = Drums(),
            ["Members"] = 2,
            ["ColorToken"] = "var(--track-1)",
            ["Palette"] = (IReadOnlyList<string>)[.. Enumerable.Repeat("#ee6d86", 12)],
            ["Actions"] = SessionActions.ReadOnly("Offline."),
        });

        Assert.Contains("Offline.", Html(strip));
        Assert.True(strip.TitleOfElementNamed("Colour 1 for group Drums", "Offline."));
        Assert.Equal("true", strip.Values("aria-current").Single());
        await strip.DisposeServicesAsync();
    }

    /// <summary>A drag on a group's fader is drawn by the fader alone, sends nothing before the release, and sends one volume on it.</summary>
    [Fact]
    public async Task AGroupFaderDragSendsOneVolumeOnRelease()
    {
        DrivenPointer pointer = new();
        List<double> sent = [];
        SessionActions actions = new()
        {
            CanEdit = true,
            Groups = new GroupActions
            {
                SetVolume = (_, volume) =>
                {
                    sent.Add(volume);
                    return Task.FromResult(EditResult.Applied(true, []));
                },
            },
        };
        Dictionary<string, object?> parameters = new()
        {
            ["Group"] = Drums(),
            ["Members"] = 2,
            ["ColorToken"] = "var(--track-1)",
            ["Palette"] = (IReadOnlyList<string>)[.. Enumerable.Repeat("#ee6d86", 12)],
            ["Actions"] = actions,
        };
        RenderedMarkup strip = await RenderedMarkup.StartAsync<GroupStrip>(parameters, services => services.AddSingleton<IPointerTracker>(pointer));

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
