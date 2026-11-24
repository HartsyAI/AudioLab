using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>What a channel strip shows of a chain, what pressing a slot opens, and that the master is honest about having no chain.</summary>
public sealed class ChannelStripEffectsTests
{
    private static readonly StripEffect[] _three =
    [
        new(EffectId.From("e1"), "EQ", true),
        new(EffectId.From("e2"), "Compressor", false),
        new(EffectId.From("e3"), "Reverb", true),
    ];

    private static Task<RenderedMarkup> StripAsync(StripEffect[] effects, bool opens = true, bool master = false)
    {
        Dictionary<string, object?> parameters = new()
        {
            ["Name"] = master ? "Master" : "Drums",
            ["ColorToken"] = "var(--track-1)",
            ["Effects"] = (IReadOnlyList<StripEffect>)effects,
            ["Master"] = master,
            ["MasterNote"] = "The master has no effect chain.",
        };
        if (!master)
        {
            parameters["Track"] = TrackId.From("t1");
        }
        if (opens)
        {
            parameters["OnOpenEffects"] = EventCallback.Factory.Create<(TrackId, EffectId?)>(new object(), _ => { });
        }
        return RenderedMarkup.OfAsync<ChannelStrip>(parameters);
    }

    /// <summary>A chain longer than the slots shows the first, and a last slot that counts the rest; both are buttons that open the chain.</summary>
    [Fact]
    public async Task ALongChainShowsTheFirstAndCountsTheRest()
    {
        RenderedMarkup strip = await StripAsync(_three);

        Assert.Contains("EQ", strip.Texts);
        Assert.Contains("2 more effects", strip.Texts);
        Assert.Contains("EQ, on", strip.Values("aria-label"));
        Assert.Contains($"EQ, on. Open in the Effects tab ({Shortcuts.OpenEffects.Display})", strip.Values("title"));
    }

    /// <summary>The slot that counts the rest says what it stands for: on while any of them is, "all off" when none is.</summary>
    [Fact]
    public async Task TheSlotThatCountsTheRestSaysWhatItStandsFor()
    {
        StripEffect[] some = [_three[0], _three[1], _three[2]];
        StripEffect[] none = [_three[0], _three[1] with { Enabled = false }, _three[2] with { Enabled = false }];

        RenderedMarkup on = await StripAsync(some);
        RenderedMarkup off = await StripAsync(none);

        Assert.Contains("2 more effects", on.Values("aria-label"));
        Assert.DoesNotContain("2 more effects, on", on.Values("aria-label"));
        Assert.Contains("2 more effects, all off", off.Values("aria-label"));
    }

    /// <summary>A bypassed effect says "off" in words, not by dimming alone, and its description says so.</summary>
    [Fact]
    public async Task ABypassedEffectSaysOffInWords()
    {
        RenderedMarkup strip = await StripAsync([_three[1]]);

        Assert.Contains("Compressor, off", strip.Values("aria-label"));
        Assert.Contains(strip.Markup, markup => markup.Contains(">off<", StringComparison.Ordinal));
        Assert.Contains("true", strip.Values("data-bypassed"));
    }

    /// <summary>An empty strip has two slots that offer to add an effect and open the chain.</summary>
    [Fact]
    public async Task AnEmptyStripOffersToAddAnEffect()
    {
        RenderedMarkup strip = await StripAsync([]);

        Assert.Equal(2, strip.Texts.Count(text => text == "Add effect"));
        Assert.Contains("Add an effect to Drums", strip.Values("aria-label"));
    }

    /// <summary>A strip with nothing to open to shows slots that are not buttons.</summary>
    [Fact]
    public async Task AStripWithNothingToOpenToShowsNoButtons()
    {
        RenderedMarkup strip = await StripAsync(_three, opens: false);

        Assert.DoesNotContain("Open in the Effects tab", string.Join(' ', strip.Values("title")), StringComparison.Ordinal);
        Assert.Contains("EQ", strip.Texts);
    }

    /// <summary>The master's limiter is shown and explained, and is not a button: the master has no chain.</summary>
    [Fact]
    public async Task TheMasterLimiterIsShownAndNotAButton()
    {
        RenderedMarkup master = await StripAsync([new StripEffect(null, "Limiter", true)], master: true);

        Assert.Contains("Limiter", master.Texts);
        Assert.Contains("The master has no effect chain.", master.Values("title"));
        Assert.DoesNotContain("Open in the Effects tab", string.Join(' ', master.Values("title")), StringComparison.Ordinal);
    }

    /// <summary>Opening from a strip selects the track and the effect, shows the effects tab, and asks the focus to follow once.</summary>
    [Fact]
    public void OpeningFromAStripSelectsShowsAndAsksForTheFocusOnce()
    {
        SessionSelection selection = new();
        EffectSelection effects = new();
        BottomPanelState panel = new();
        EffectsNavigator navigator = new(selection, effects, panel);

        navigator.Open(TrackId.From("t2"), EffectId.From("e9"));

        Assert.Equal(TrackId.From("t2"), selection.Track);
        Assert.Equal(EffectId.From("e9"), effects.Effect);
        Assert.Equal(BottomPanelState.Effects, panel.Active);
        Assert.True(effects.TakeFocusRequest());
        Assert.False(effects.TakeFocusRequest());
    }

    /// <summary>Opening the chain of the track that is already selected keeps the selected clip.</summary>
    [Fact]
    public void OpeningTheChainOfTheSelectedTrackKeepsTheClip()
    {
        SessionSelection selection = new();
        selection.SelectClip(TrackId.From("t1"), ClipId.From("c1"));
        EffectsNavigator navigator = new(selection, new EffectSelection(), new BottomPanelState());

        navigator.Open(TrackId.From("t1"), null);

        Assert.Equal(ClipId.From("c1"), selection.Clip);
    }

    /// <summary>Asking for the focus tells an open panel at once, so it does not wait for an unrelated draw to spend the request.</summary>
    [Fact]
    public void AskingForTheFocusTellsAnOpenPanelAtOnce()
    {
        EffectSelection effects = new();
        int told = 0;
        effects.Changed += () => told++;

        effects.RequestFocus();

        Assert.Equal(1, told);
        Assert.True(effects.TakeFocusRequest());
    }

    /// <summary>Opening a track's chain with no effect named leaves the chosen effect as it was.</summary>
    [Fact]
    public void OpeningAChainWithNoEffectLeavesTheChoiceAlone()
    {
        EffectSelection effects = new();
        effects.Select(EffectId.From("e4"));
        EffectsNavigator navigator = new(new SessionSelection(), effects, new BottomPanelState());

        navigator.Open(TrackId.From("t1"), null);

        Assert.Equal(EffectId.From("e4"), effects.Effect);
    }

    /// <summary>The selected effect is the chosen one when the chain holds it, else the first, else none.</summary>
    [Fact]
    public void TheSelectedEffectFallsBackToTheFirst()
    {
        EffectSelection effects = new();
        Track track = Song.NewTrack("t1") with
        {
            Effects = [.. _three.Select(effect => EffectRules.Create(EffectKind.Eq, effect.Id!.Value))],
        };

        Assert.Equal(EffectId.From("e1"), effects.Of(track)!.Id);
        effects.Select(EffectId.From("e3"));
        Assert.Equal(EffectId.From("e3"), effects.Of(track)!.Id);
        effects.Select(EffectId.From("gone"));
        Assert.Equal(EffectId.From("e1"), effects.Of(track)!.Id);
        Assert.Null(effects.Of(Song.NewTrack("t2")));
    }

    /// <summary>A panel opened by a strip moves the keyboard to the effect that was asked for.</summary>
    [Fact]
    public async Task APanelOpenedByAStripMovesTheKeyboardToTheEffect()
    {
        RecordingFocus focus = new();
        EffectSelection effects = new();
        effects.Select(EffectId.From("e2"));
        effects.RequestFocus();
        Track track = Song.NewTrack("t1") with
        {
            Effects = [.. _three.Select(effect => EffectRules.Create(EffectKind.Eq, effect.Id!.Value))],
        };

        await RenderedMarkup.OfAsync<EffectsPanel>(
            new Dictionary<string, object?>
            {
                ["Document"] = Song.Empty().With(track),
                ["Selected"] = track.Id,
                ["Actions"] = SessionActions.ReadOnly("Offline."),
            },
            services =>
            {
                services.AddSingleton<IKeyGuard, NoGuard>();
                services.AddSingleton<IElementView, NoView>();
                services.AddSingleton<IElementFocus>(focus);
                services.AddSingleton(effects);
                services.AddScoped(_ => new ToastService(TimeProvider.System));
            });

        Assert.Equal(["effect-row-e2"], focus.Asked);
        Assert.False(effects.TakeFocusRequest());
    }

    private sealed class RecordingFocus : IElementFocus
    {
        public List<string> Asked { get; } = [];

        public void Focus(string elementId) => Asked.Add(elementId);

        public void FocusIn(string containerId, string selector) => Asked.Add($"{containerId} {selector}");
    }

    private sealed class NoGuard : IKeyGuard
    {
        public void PreventNavigationKeys(string containerId, string targetRole)
        {
        }
    }

    private sealed class NoView : IElementView
    {
        public (double Left, double Top, double Width, double Height)? Bounds(string elementId) => null;

        public (double Left, double Top)? Scrolled(string elementId) => null;

        public void ScrollTo(string elementId, double left, double top)
        {
        }

        public bool HasFocusWithin(string elementId) => false;
    }
}
