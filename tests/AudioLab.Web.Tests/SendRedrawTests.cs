using System.Globalization;
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

/// <summary>What the sends of a track show, what a change to one send redraws, and how a send drag is sent.</summary>
public sealed class SendRedrawTests
{
    // Static text is written as markup and dynamic text as a text frame, so an assertion reads both.
    private static string Html(RenderedMarkup rendered) => string.Join(" ", rendered.Markup.Concat(rendered.Texts));

    private static Track Bus(string id, string name) => Song.NewTrack(id) with { Name = name, Kind = TrackKind.Bus };

    private static Track Guitar(params Send[] sends) => Song.NewTrack("guitar") with { Name = "Guitar", Sends = [.. sends] };

    private static ProjectDocument Mix(params Track[] tracks) => Song.Empty().With(tracks);

    private static Dictionary<string, object?> PanelParameters(ProjectDocument document, TrackId? selected)
    {
        Track? track = selected is { } id ? document.Tracks.FirstOrDefault(candidate => candidate.Id == id) : null;
        return new Dictionary<string, object?>
        {
            ["Document"] = document,
            ["Actions"] = SessionActions.ReadOnly("Offline."),
            ["Selected"] = track,
        };
    }

    private static Dictionary<string, object?> MixerParameters(ProjectDocument document)
    {
        return new Dictionary<string, object?>
        {
            ["Document"] = document,
            ["Actions"] = SessionActions.ReadOnly("Offline."),
            ["Selected"] = TrackId.From("guitar"),
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

    /// <summary>Each send names its bus, and its level is a knob that says which send it sets.</summary>
    [Fact]
    public async Task ASendNamesItsBusAndItsKnob()
    {
        ProjectDocument document = Mix(Bus("reverb", "Reverb"), Guitar(new Send(TrackId.From("reverb"), 0.5, false, false)));

        RenderedMarkup panel = await RenderedMarkup.OfAsync<SendsPanel>(PanelParameters(document, TrackId.From("guitar")), MixerServices);

        Assert.Contains("Level of the send from Guitar to Reverb", panel.Values("aria-label"));
        Assert.Contains("-6.0 dB", Html(panel));
    }

    /// <summary>A bus has no sends to show, and a track with no bus to send to says it needs one.</summary>
    [Fact]
    public async Task ABusHasNoSendsAndATrackWithoutABusSaysSo()
    {
        ProjectDocument withoutBus = Mix(Guitar());

        RenderedMarkup noBus = await RenderedMarkup.OfAsync<SendsPanel>(PanelParameters(withoutBus, TrackId.From("guitar")), MixerServices);
        RenderedMarkup bus = await RenderedMarkup.OfAsync<SendsPanel>(
            PanelParameters(Mix(Bus("reverb", "Reverb")), TrackId.From("reverb")),
            MixerServices);

        Assert.Contains("There is no bus to send to. Add a bus track first.", Html(noBus));
        Assert.Contains("A bus has no sends.", Html(bus));
    }

    /// <summary>A send menu that cannot act is a disabled button, and the reason is written as text beside it.</summary>
    [Fact]
    public async Task AnAddSendThatCannotActSaysWhy()
    {
        ProjectDocument document = Mix(Bus("reverb", "Reverb"), Guitar());

        RenderedMarkup panel = await RenderedMarkup.OfAsync<SendsPanel>(PanelParameters(document, TrackId.From("guitar")), MixerServices);

        Assert.Contains("Offline.", Html(panel));
        Assert.Contains(panel.Values("title"), title => title.Contains("Offline.", StringComparison.Ordinal));
    }

    /// <summary>A change to one send's level draws that send's row and its knob, and no strip of the mixer.</summary>
    [Fact]
    public async Task AChangedLevelDrawsOnlyItsRow()
    {
        ProjectDocument before = Mix(
            Bus("reverb", "Reverb"),
            Bus("delay", "Delay"),
            Guitar(new Send(TrackId.From("reverb"), 0.5, false, false), new Send(TrackId.From("delay"), 0.5, false, false)));
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(before), MixerServices);

        await mixer.RenderAgainAsync(MixerParameters(Mix(
            Bus("reverb", "Reverb"),
            Bus("delay", "Delay"),
            Guitar(new Send(TrackId.From("reverb"), 1, false, false), new Send(TrackId.From("delay"), 0.5, false, false)))));

        Assert.Equal(0, mixer.Drawn.Count(type => type == typeof(ChannelStrip)));
        Assert.Equal(1, mixer.Drawn.Count(type => type == typeof(SendRow)));
        Assert.Equal(1, mixer.Drawn.Count(type => type == typeof(Knob)));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>A mixer given the same document again draws no send.</summary>
    [Fact]
    public async Task AMixerGivenTheSameDocumentDrawsNoSendAgain()
    {
        ProjectDocument document = Mix(Bus("reverb", "Reverb"), Guitar(new Send(TrackId.From("reverb"), 0.5, false, false)));
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(document), MixerServices);

        await mixer.RenderAgainAsync(MixerParameters(document));

        Assert.Equal(0, mixer.Drawn.Count(type => type == typeof(SendRow)));
        Assert.Equal(0, mixer.Drawn.Count(type => type == typeof(SendsPanel)));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>A drag on a send's knob is drawn by the knob alone, sends nothing before the release, and sends one level on it.</summary>
    [Fact]
    public async Task ASendKnobDragSendsOneLevelOnRelease()
    {
        DrivenPointer pointer = new();
        List<double> sent = [];
        SessionActions actions = new()
        {
            CanEdit = true,
            Sends = new SendActions
            {
                SetLevel = (_, _, level) =>
                {
                    sent.Add(level);
                    return Task.FromResult(EditResult.Applied(true, []));
                },
            },
        };
        Dictionary<string, object?> parameters = new()
        {
            ["Track"] = TrackId.From("guitar"),
            ["TrackName"] = "Guitar",
            ["Send"] = new Send(TrackId.From("reverb"), 0.5, false, false),
            ["BusName"] = "Reverb",
            ["Actions"] = actions,
        };
        RenderedMarkup row = await RenderedMarkup.StartAsync<SendRow>(parameters, services => services.AddSingleton<IPointerTracker>(pointer));

        await row.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        List<int> drawnPerMove = [];
        for (int step = 1; step <= 40; step++)
        {
            await row.RunAsync(() => pointer.Move(0, 300 - (step * 2)));
            await Task.Yield();
            drawnPerMove.Add(row.Drawn.Count);
        }

        Assert.All(drawnPerMove, count => Assert.Equal(1, count));
        Assert.All(row.Drawn, type => Assert.Equal(typeof(Knob), type));
        Assert.Empty(sent);
        await row.RunAsync(() => pointer.Release(0, 220));
        await UntilAsync(() => sent.Count == 1);
        Assert.Single(sent);
        await row.DisposeServicesAsync();
    }

    /// <summary>The decibel readouts are written with a point, whatever the culture of the browser.</summary>
    [Fact]
    public void TheReadoutsIgnoreTheCulture()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("-6.0 dB", FaderScale.Readout(0.5));
            Assert.Equal("+6.0 dB", FaderScale.Readout(2));
            Assert.Equal("-inf", FaderScale.Readout(0));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>The fader's scale and its inverse agree: a position read from a gain, written back, gives the same position.</summary>
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void APositionWrittenBackFromItsGainIsTheSamePosition(double position)
    {
        double gain = FaderScale.Gain(position);

        Assert.Equal(position, FaderScale.Position(gain), 6);
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
