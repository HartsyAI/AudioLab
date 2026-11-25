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

    private static Dictionary<string, object?> MixerParameters(ProjectDocument document)
    {
        return new Dictionary<string, object?>
        {
            ["Document"] = document,
            ["Actions"] = SessionActions.ReadOnly("Offline."),
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

    /// <summary>A fader drag is drawn by the fader alone, and nothing is sent until the release.</summary>
    [Fact]
    public async Task AFaderDragIsDrawnByTheFaderAloneAndSendsNothingBeforeTheRelease()
    {
        DrivenPointer pointer = new();
        List<double> sent = [];
        Dictionary<string, object?> parameters = new()
        {
            ["Label"] = "Volume of group Drums",
            ["Text"] = "0.0 dB",
            ["Position"] = 0.75,
            ["OnCommit"] = EventCallback.Factory.Create<double>(new object(), position => sent.Add(position)),
        };
        RenderedMarkup fader = await RenderedMarkup.StartAsync<Fader>(parameters, services => services.AddSingleton<IPointerTracker>(pointer));

        await fader.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        List<int> drawnPerMove = [];
        for (int step = 1; step <= 40; step++)
        {
            await fader.RunAsync(() => pointer.Move(0, 300 - (step * 2)));
            await Task.Yield();
            drawnPerMove.Add(fader.Drawn.Count);
        }

        Assert.All(drawnPerMove, count => Assert.Equal(1, count));
        Assert.All(fader.Drawn, type => Assert.Equal(typeof(Fader), type));
        Assert.Empty(sent);
        await fader.RunAsync(() => pointer.Release(0, 220));
        await Task.Delay(50);
        Assert.Single(sent);
        await fader.DisposeServicesAsync();
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
