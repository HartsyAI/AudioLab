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

    /// <summary>A drag moves the knob alone: no other component is drawn for any move, and nothing is sent before the release.</summary>
    [Fact]
    public async Task ADragDrawsTheKnobAloneAndSendsNothingBeforeTheRelease()
    {
        DrivenPointer pointer = new();
        List<double> sent = [];
        RenderedMarkup knob = await RenderedMarkup.StartAsync<Knob>(
            new Dictionary<string, object?>
            {
                ["Label"] = "Mid gain of EQ 1",
                ["Position"] = 0.0,
                ["Text"] = "0 dB",
                ["Format"] = (Func<double, string>)(position => position.ToString("0.00", CultureInfo.InvariantCulture)),
                ["OnCommit"] = EventCallback.Factory.Create<double>(new object(), position => sent.Add(position)),
            },
            services => services.AddSingleton<IPointerTracker>(pointer));

        await knob.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        List<int> drawnPerMove = [];
        for (int step = 1; step <= 40; step++)
        {
            await knob.RunAsync(() => pointer.Move(0, 300 - (step * 2)));
            await Task.Yield();
            drawnPerMove.Add(knob.Drawn.Count);
        }

        Assert.All(drawnPerMove, count => Assert.Equal(1, count));
        Assert.All(knob.Drawn, type => Assert.Equal(typeof(Knob), type));
        Assert.Empty(sent);
        await knob.RunAsync(() => pointer.Release(0, 220));
        await Task.Delay(50);
        Assert.Single(sent);
        // The knob was at the middle; the 80 pixels of the drag are counted from where it passed the 3-pixel threshold, which is 2 pixels in.
        Assert.Equal(((0.5 + (78 / ValueGesture.TravelPixels)) * 2) - 1, sent[0], 6);
        await knob.DisposeServicesAsync();
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
