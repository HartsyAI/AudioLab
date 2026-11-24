using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Components.Effects;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session.Effects;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>What the effects panel offers to the keyboard and says to assistive technology, and why a control does nothing.</summary>
public sealed class EffectsPanelMarkupTests
{
    private const string Offline = "Reconnecting to the server.";

    private static Track WithEffects(int count)
    {
        Track track = Song.NewTrack("t1");
        EffectId[] ids = [.. Enumerable.Range(0, count).Select(index => EffectId.From($"e{index}"))];
        return track with { Effects = [.. ids.Select(id => EffectRules.Create(EffectKind.Eq, id))] };
    }

    private static Task<RenderedMarkup> RowAsync(int index, int count, bool editable, string? reason = null)
    {
        return RenderedMarkup.OfAsync<EffectRow>(new Dictionary<string, object?>
        {
            ["Effect"] = EffectRules.Create(EffectKind.Reverb, EffectId.From("e1")),
            ["Index"] = index,
            ["Count"] = count,
            ["Editable"] = editable,
            ["Reason"] = reason,
        });
    }

    // The add menu is the library's, which needs the browser to render; a panel that cannot add shows a button in its place, and that is rendered.
    private static Task<RenderedMarkup> PanelAsync(Track? track, bool editable)
    {
        ProjectDocument document = Song.Empty().With(track is null ? [] : [track]);
        return RenderedMarkup.OfAsync<EffectsPanel>(
            new Dictionary<string, object?>
            {
                ["Document"] = document,
                ["Selected"] = track?.Id,
                ["Actions"] = editable ? new SessionActions { CanEdit = true } : SessionActions.ReadOnly(Offline),
            },
            services =>
            {
                services.AddSingleton<IKeyGuard, NoGuard>();
                services.AddScoped<EffectSelection>();
                services.AddSingleton<IElementView, NoView>();
                services.AddSingleton<IElementFocus, NoFocus>();
                services.AddScoped(_ => new ToastService(TimeProvider.System));
            });
    }

    /// <summary>A row is named by its effect and its place, and says whether it is on, so the list can be read by ear.</summary>
    [Fact]
    public async Task ARowIsNamedByItsEffectAndItsPlace()
    {
        RenderedMarkup row = await RowAsync(1, 3, true);

        Assert.Contains("Reverb, effect 2 of 3, on", row.Values("aria-label"));
        Assert.Contains("Reverb 2 on", row.Values("aria-label"));
        Assert.Contains("Move Reverb 2 earlier", row.Values("aria-label"));
        Assert.Contains("Move Reverb 2 later", row.Values("aria-label"));
        Assert.Contains("Remove Reverb 2", row.Values("aria-label"));
        Assert.Contains("0", row.Values("tabindex"));
    }

    /// <summary>Every button of a row shows its shortcut from the registry, and none writes a chord of its own.</summary>
    [Fact]
    public async Task EveryButtonOfARowShowsItsShortcutFromTheRegistry()
    {
        RenderedMarkup row = await RowAsync(1, 3, true);

        string[] titles = row.Values("title");
        Assert.Contains($"Move Reverb 2 earlier ({Shortcuts.EffectEarlier.Display})", titles);
        Assert.Contains($"Move Reverb 2 later ({Shortcuts.EffectLater.Display})", titles);
        Assert.Contains($"Remove Reverb 2 ({Shortcuts.EffectRemove.Display})", titles);
    }

    /// <summary>The first effect cannot go earlier and says why; the last cannot go later and says why.</summary>
    [Fact]
    public async Task TheEndsOfTheChainSayWhyTheyCannotMove()
    {
        RenderedMarkup first = await RowAsync(0, 3, true);
        RenderedMarkup last = await RowAsync(2, 3, true);

        Assert.Contains("It is already the first effect.", first.Values("title"));
        Assert.DoesNotContain("It is already the last effect.", first.Values("title"));
        Assert.Contains("It is already the last effect.", last.Values("title"));
    }

    /// <summary>A row that cannot edit says why in the title of every control, and its drag handle says so as well.</summary>
    [Fact]
    public async Task ARowThatCannotEditSaysWhyInEveryControl()
    {
        RenderedMarkup row = await RowAsync(1, 3, false, Offline);

        Assert.True(row.Values("title").Count(title => title == Offline) >= 4);
        Assert.Contains(Offline, row.Values("title"));
        Assert.Contains("false", row.Values("data-editable"));
    }

    /// <summary>A bypassed effect says so in words and in its description, not by dimming alone.</summary>
    [Fact]
    public async Task ABypassedEffectSaysSoInWords()
    {
        RenderedMarkup row = await RenderedMarkup.OfAsync<EffectRow>(new Dictionary<string, object?>
        {
            ["Effect"] = EffectRules.Create(EffectKind.Reverb, EffectId.From("e1")) with { Enabled = false },
            ["Index"] = 0,
            ["Count"] = 1,
            ["Editable"] = true,
        });

        Assert.Contains("Reverb, effect 1 of 1, bypassed", row.Values("aria-label"));
        Assert.Contains("Bypassed", row.Texts);
        Assert.Contains("false", row.Values("aria-pressed"));
    }

    /// <summary>With no track selected the panel says so, and offers no effect controls.</summary>
    [Fact]
    public async Task WithNoTrackSelectedThePanelSaysSo()
    {
        RenderedMarkup panel = await PanelAsync(null, true);

        Assert.Contains("No track selected", panel.Texts);
        Assert.DoesNotContain("Add effect", panel.Texts);
    }

    /// <summary>An empty chain offers the three common effects, and says why they do nothing when the project cannot be edited.</summary>
    [Fact]
    public async Task AnEmptyChainOffersTheCommonEffects()
    {
        RenderedMarkup panel = await PanelAsync(WithEffects(0), false);

        Assert.Contains("No effects", panel.Texts);
        Assert.Contains("EQ", panel.Texts);
        Assert.Contains("Reverb", panel.Texts);
        Assert.Equal(4, panel.Values("title").Count(title => title == Offline));
    }

    /// <summary>A full chain shows the count against the limit and a row for every effect; the words of its refusal are Core's.</summary>
    [Fact]
    public async Task AFullChainShowsTheCountAgainstTheLimit()
    {
        RenderedMarkup panel = await PanelAsync(WithEffects(ProjectLimits.MaxEffects), false);

        Assert.Contains($"{ProjectLimits.MaxEffects} of {ProjectLimits.MaxEffects}", panel.Texts);
        Assert.Contains(EffectRules.AddRefusal(ProjectLimits.MaxEffects), panel.Values("title"));
        Assert.Equal(ProjectLimits.MaxEffects, panel.Values("role").Count(role => role == "group"));
    }

    /// <summary>A project that cannot be edited disables adding with the reason the screen gave.</summary>
    [Fact]
    public async Task AProjectThatCannotBeEditedDisablesAddingWithTheReason()
    {
        RenderedMarkup panel = await PanelAsync(WithEffects(2), false);

        Assert.Contains(Offline, panel.Values("title"));
        Assert.Contains("true", panel.Values("aria-disabled"));
    }

    /// <summary>The list is named, in signal order, and a change is announced in a status region.</summary>
    [Fact]
    public async Task TheListIsNamedAndChangesAreAnnounced()
    {
        RenderedMarkup panel = await PanelAsync(WithEffects(2), false);

        Assert.Contains("Effects of t1, in signal order", panel.Values("aria-label"));
        Assert.Contains("status", panel.Values("role"));
    }

    private sealed class NoFocus : IElementFocus
    {
        public void Focus(string elementId)
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

    private sealed class NoGuard : IKeyGuard
    {
        public void PreventNavigationKeys(string containerId, string targetRole)
        {
        }
    }
}
