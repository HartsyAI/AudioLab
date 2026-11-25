using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>What the sends of a track show, and what a change to one send redraws.</summary>
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
        RenderedMarkup bus = await RenderedMarkup.OfAsync<SendsPanel>(PanelParameters(Mix(Bus("reverb", "Reverb")), TrackId.From("reverb")), MixerServices);

        Assert.Contains("There is no bus to send to. Add a bus track first.", Html(noBus));
        Assert.Contains("A bus has no sends.", Html(bus));
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
        RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(MixerParameters(Mix(Bus("reverb", "Reverb"), Guitar(new Send(TrackId.From("reverb"), 0.5, false, false)))), MixerServices);

        await mixer.RenderAgainAsync(MixerParameters(Mix(Bus("reverb", "Reverb"), Guitar(new Send(TrackId.From("reverb"), 0.5, false, false)))));

        Assert.Equal(0, mixer.Drawn.Count(type => type == typeof(SendRow)));
        Assert.Equal(0, mixer.Drawn.Count(type => type == typeof(SendsPanel)));
        await mixer.DisposeServicesAsync();
    }

    /// <summary>The fader's scale and its inverse agree, so a knob that reads a level writes back the same one.</summary>
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void TheScaleAndItsInverseAgree(double position)
    {
        double gain = FaderScale.Gain(position);

        Assert.Equal(position, FaderScale.Position(gain), 6);
    }
}
