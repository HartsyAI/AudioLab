using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Components.Effects;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>What a change to an effect redraws: a drag the knob alone, an edit of one track only its strip, and nothing for nothing.</summary>
public sealed class EffectRedrawTests
{
    private static ProjectDocument Sixteen(Func<int, Track, Track>? edit = null)
    {
        Track[] tracks =
        [
            .. Enumerable.Range(1, 16).Select(number =>
            {
                Track track = Song.NewTrack($"t{number}") with
                {
                    Effects = [.. Enumerable.Range(1, 3).Select(place => EffectRules.Create(EffectKind.Eq, EffectId.From($"t{number}e{place}")))],
                };
                return edit?.Invoke(number, track) ?? track;
            }),
        ];
        return Song.Empty().With(tracks);
    }

    private static Dictionary<string, object?> MixerParameters(ProjectDocument document)
    {
        return new Dictionary<string, object?> { ["Document"] = document, ["Actions"] = SessionActions.ReadOnly("Offline.") };
    }

    private static void MixerServices(IServiceCollection services)
    {
        services.AddSingleton<AudioLab.Web.Interop.IKeyGuard>(new NoKeyGuard());
        services.AddSingleton(new TrackColorMap([.. Enumerable.Repeat("#ee6d86", 12)]));
        services.AddScoped<SessionSelection>();
        services.AddScoped<EffectSelection>();
        services.AddScoped<BottomPanelState>();
        services.AddScoped<EffectsNavigator>();
    }

    /// <summary>A mixer of 16 tracks with 3 effects each, given the document it already has, draws no strip again.</summary>
    [Fact]
    public async Task AMixerGivenTheSameDocumentDrawsNoStripAgain()
    {
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Sixteen()), MixerServices);

        await mixer.RenderAgainAsync(MixerParameters(Sixteen()));

        Assert.Equal(0, mixer.Drawn.Count(type => type == typeof(ChannelStrip)));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>Bypassing one effect of one track draws that track's strip and no other.</summary>
    [Fact]
    public async Task BypassingOneEffectDrawsOneStrip()
    {
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Sixteen()), MixerServices);

        await mixer.RenderAgainAsync(MixerParameters(Sixteen((number, track) =>
            number == 5 ? track with { Effects = [track.Effects[0], track.Effects[1] with { Enabled = false }, track.Effects[2]] } : track)));

        Assert.Equal(1, mixer.Drawn.Count(type => type == typeof(ChannelStrip)));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>Changing a setting of an effect, which the strip does not show, draws no strip.</summary>
    [Fact]
    public async Task ChangingASettingDrawsNoStrip()
    {
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Sixteen()), MixerServices);

        await mixer.RenderAgainAsync(MixerParameters(Sixteen((number, track) =>
            number == 5 ? track with { Effects = [((EqEffect)track.Effects[0]) with { MidGain = 6 }, track.Effects[1], track.Effects[2]] } : track)));

        Assert.Equal(0, mixer.Drawn.Count(type => type == typeof(ChannelStrip)));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>A change to one setting draws that knob and no other knob of the effect.</summary>
    [Fact]
    public async Task AChangeToOneSettingDrawsOneKnob()
    {
        EqEffect eq = (EqEffect)EffectRules.Create(EffectKind.Eq, EffectId.From("e1"));
        Dictionary<string, object?> Parameters(Effect effect) =>
            new() { ["Effect"] = effect, ["Index"] = 0, ["Editable"] = false, ["Reason"] = "Offline." };
        RenderedMarkup settings = await RenderedMarkup.StartAsync<EffectSettings>(Parameters(eq));

        await settings.RenderAgainAsync(Parameters(eq with { MidGain = 6 }));

        Assert.Equal(1, settings.Drawn.Count(type => type == typeof(EffectParameterControl)));
        Assert.Equal(1, settings.Drawn.Count(type => type == typeof(Knob)));
        await settings.DisposeServicesAsync();
    }

    private static Dictionary<string, object?> ControlParameters(List<(string Name, double Value)> sent)
    {
        EqEffect eq = (EqEffect)EffectRules.Create(EffectKind.Eq, EffectId.From("e1"));
        return new Dictionary<string, object?>
        {
            ["Setting"] = EffectRules.Find(eq, "midGain"),
            ["Effect"] = eq,
            ["Subject"] = "EQ 1",
            ["OnSet"] = EventCallback.Factory.Create<(string Name, double Value)>(new object(), change => sent.Add(change)),
        };
    }

    /// <summary>A drag is drawn by the knob alone: in the control that owns it nothing else is drawn for any move, and nothing is sent early.</summary>
    [Fact]
    public async Task ADragIsDrawnByTheKnobAloneAndSendsNothingBeforeTheRelease()
    {
        DrivenPointer pointer = new();
        List<(string Name, double Value)> sent = [];
        RenderedMarkup control = await RenderedMarkup.StartAsync<EffectParameterControl>(
            ControlParameters(sent),
            services => services.AddSingleton<IPointerTracker>(pointer));

        await control.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        List<int> drawnPerMove = [];
        for (int step = 1; step <= 40; step++)
        {
            await control.RunAsync(() => pointer.Move(0, 300 - (step * 2)));
            await Task.Yield();
            drawnPerMove.Add(control.Drawn.Count);
        }

        Assert.All(drawnPerMove, count => Assert.Equal(1, count));
        Assert.All(control.Drawn, type => Assert.Equal(typeof(Knob), type));
        Assert.Empty(sent);
        await control.RunAsync(() => pointer.Release(0, 220));
        await Task.Delay(50);
        // The knob was at the middle of -12 to 12; the 80 pixels are counted from where the drag passed the 3-pixel threshold, 2 pixels in.
        (string name, double value) = Assert.Single(sent);
        Assert.Equal("midGain", name);
        Assert.Equal(-12 + (24 * (0.5 + (78 / ValueGesture.TravelPixels))), value, 6);
        await control.DisposeServicesAsync();
    }

    /// <summary>A key that would change nothing sends nothing: Home at the lowest value, End at the highest, Backspace at the default.</summary>
    [Fact]
    public async Task AKeyThatWouldChangeNothingSendsNothing()
    {
        List<double> sent = [];
        RenderedMarkup knob = await RenderedMarkup.StartAsync<Knob>(
            new Dictionary<string, object?>
            {
                ["Label"] = "Mid gain",
                ["Position"] = -1.0,
                ["DefaultPosition"] = -1.0,
                ["Text"] = "-12 dB",
                ["OnCommit"] = EventCallback.Factory.Create<double>(new object(), position => sent.Add(position)),
            });

        await knob.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "Home" });
        await knob.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "Backspace" });
        Assert.Empty(sent);
        await knob.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "End" });
        await Task.Delay(50);

        Assert.Equal([1.0], sent);
        await knob.DisposeServicesAsync();
    }

    /// <summary>A knob that goes away in the middle of a drag says so, and one that goes away at rest does not.</summary>
    [Fact]
    public async Task AKnobThatGoesAwayInTheMiddleOfADragSaysSo()
    {
        int abandoned = 0;
        DrivenPointer pointer = new();
        Dictionary<string, object?> Parameters() => new()
        {
            ["Label"] = "Mid gain",
            ["Position"] = 0.0,
            ["Text"] = "0 dB",
            ["OnCommit"] = EventCallback.Factory.Create<double>(new object(), _ => { }),
            ["OnAbandon"] = EventCallback.Factory.Create(new object(), () => abandoned++),
        };
        RenderedMarkup resting = await RenderedMarkup.StartAsync<Knob>(Parameters());
        RenderedMarkup dragged = await RenderedMarkup.StartAsync<Knob>(Parameters(), services => services.AddSingleton<IPointerTracker>(pointer));

        await dragged.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        await dragged.RunAsync(() => pointer.Move(0, 280));
        resting.TakeDown();
        Assert.Equal(0, abandoned);
        dragged.TakeDown();

        Assert.Equal(1, abandoned);
        await resting.DisposeServicesAsync();
        await dragged.DisposeServicesAsync();
    }

    /// <summary>Showing another effect makes new knobs, even of the same kind, so no knob carries a drag over to it.</summary>
    [Fact]
    public async Task ShowingAnotherEffectMakesNewKnobsEvenOfTheSameKind()
    {
        Track track = Song.NewTrack("t1") with
        {
            Effects = [EffectRules.Create(EffectKind.Reverb, EffectId.From("a")), EffectRules.Create(EffectKind.Reverb, EffectId.From("b"))],
        };
        EffectSelection selection = new();
        selection.Select(EffectId.From("a"));
        RenderedMarkup panel = await RenderedMarkup.StartAsync<EffectsPanel>(
            new Dictionary<string, object?>
            {
                ["Document"] = Song.Empty().With(track),
                ["Selected"] = track.Id,
                ["Actions"] = SessionActions.ReadOnly("Offline."),
            },
            services =>
            {
                NoElementServices.Add(services);
                services.AddSingleton(selection);
                services.AddScoped(_ => new ToastService(TimeProvider.System));
            });
        int before = panel.Made(typeof(EffectParameterControl));

        await panel.RunAsync(() => selection.Select(EffectId.From("b")));

        Assert.Equal(EffectRules.Parameters(EffectKind.Reverb).Count, before);
        Assert.Equal(2 * before, panel.Made(typeof(EffectParameterControl)));
        await panel.DisposeServicesAsync();
    }

    /// <summary>The effects panel and its settings under German write no comma into a number the browser reads.</summary>
    [Fact]
    public async Task TheSettingsWriteNoCommaUnderGerman()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            foreach (EffectKind kind in Enum.GetValues<EffectKind>())
            {
                RenderedMarkup settings = await RenderedMarkup.OfAsync<EffectSettings>(new Dictionary<string, object?>
                {
                    ["Effect"] = EffectRules.Create(kind, EffectId.From("e1")),
                    ["Index"] = 0,
                    ["Editable"] = false,
                    ["Reason"] = "Offline.",
                });
                string[] numeric = [.. settings.Values("aria-valuenow"), .. settings.Values("transform"), .. settings.Values("aria-valuetext")];

                Assert.NotEmpty(numeric);
                Assert.All(numeric, value => Assert.DoesNotContain(',', value));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
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
