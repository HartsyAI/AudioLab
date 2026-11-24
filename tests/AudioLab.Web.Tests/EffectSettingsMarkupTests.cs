using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Components.Effects;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Tests;

/// <summary>What the knobs of an effect offer to the keyboard and say to assistive technology, and where their words come from.</summary>
public sealed class EffectSettingsMarkupTests
{
    private const string Offline = "Reconnecting to the server.";

    private static Task<RenderedMarkup> KnobAsync(bool interactive, bool disabled = false, string? reason = null)
    {
        Dictionary<string, object?> parameters = new()
        {
            ["Label"] = "Mid gain of EQ 1",
            ["Position"] = 0.5,
            ["Text"] = "+6 dB",
            ["Disabled"] = disabled,
            ["Reason"] = reason,
        };
        if (interactive)
        {
            parameters["OnCommit"] = EventCallback.Factory.Create<double>(new object(), _ => { });
        }
        return RenderedMarkup.OfAsync<Knob>(parameters);
    }

    private static Task<RenderedMarkup> SettingsAsync(Effect effect, bool editable)
    {
        return RenderedMarkup.OfAsync<EffectSettings>(
            new Dictionary<string, object?> { ["Effect"] = effect, ["Index"] = 1, ["Editable"] = editable, ["Reason"] = editable ? null : Offline });
    }

    /// <summary>A knob that only shows a value stays an image with its words, as the pan of a strip is.</summary>
    [Fact]
    public async Task AKnobThatOnlyShowsAValueStaysAnImage()
    {
        RenderedMarkup knob = await KnobAsync(false);

        Assert.Contains("img", knob.Values("role"));
        Assert.Contains("Mid gain of EQ 1 +6 dB", knob.Values("aria-label"));
        Assert.DoesNotContain("slider", knob.Values("role"));
    }

    /// <summary>A knob that can be changed is a slider with a name, a place and its value in words, and one tab stop.</summary>
    [Fact]
    public async Task AKnobThatCanBeChangedIsASliderWithAValueInWords()
    {
        RenderedMarkup knob = await KnobAsync(true);

        Assert.Contains("slider", knob.Values("role"));
        Assert.Contains("Mid gain of EQ 1", knob.Values("aria-label"));
        Assert.Contains("+6 dB", knob.Values("aria-valuetext"));
        Assert.Contains("75", knob.Values("aria-valuenow"));
        Assert.Contains("0", knob.Values("aria-valuemin"));
        Assert.Contains("100", knob.Values("aria-valuemax"));
        Assert.Contains("button", knob.Values("type"));
    }

    /// <summary>A knob that does nothing says why in its title and in its state, and stays focusable.</summary>
    [Fact]
    public async Task ADisabledKnobSaysWhy()
    {
        RenderedMarkup knob = await KnobAsync(true, disabled: true, reason: Offline);

        Assert.Contains(Offline, knob.Values("title"));
        Assert.Contains("true", knob.Values("aria-disabled"));
        Assert.DoesNotContain("disabled", knob.Attributes.Select(attribute => attribute.Name));
    }

    /// <summary>The pointer of the knob turns as far as its place says, with a point under any culture.</summary>
    [Fact]
    public async Task ThePointerTurnsWithAPointUnderGerman()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            RenderedMarkup knob = await KnobAsync(true);

            Assert.Contains("rotate(67.5 12 12)", knob.Values("transform"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>Each kind shows a knob for every setting Core lists, named with Core's label and the effect's place.</summary>
    [Fact]
    public async Task EachKindShowsAKnobForEverySettingCoreLists()
    {
        foreach (EffectKind kind in Enum.GetValues<EffectKind>())
        {
            RenderedMarkup settings = await SettingsAsync(EffectRules.Create(kind, EffectId.From("e1")), false);
            string name = $"{(kind == EffectKind.Eq ? "EQ" : kind.ToString())} 2";

            foreach (EffectParameter setting in EffectRules.Parameters(kind))
            {
                Assert.Contains(setting.Label, settings.Texts);
                Assert.Contains($"{setting.Label} of {name}", settings.Values("aria-label"));
            }
            Assert.Equal(EffectRules.Parameters(kind).Count, settings.Values("role").Count(role => role == "slider"));
        }
    }

    /// <summary>A knob shows the value a new effect has, written with its unit.</summary>
    [Fact]
    public async Task AKnobShowsTheValueANewEffectHas()
    {
        RenderedMarkup settings = await SettingsAsync(EffectRules.Create(EffectKind.Compressor, EffectId.From("e1")), false);

        Assert.Contains("-24 dB", settings.Values("aria-valuetext"));
        Assert.Contains("4:1", settings.Values("aria-valuetext"));
        Assert.Contains("3 ms", settings.Values("aria-valuetext"));
    }

    /// <summary>A synced delay shows its six divisions with the chosen one marked, and a free one shows none.</summary>
    [Fact]
    public async Task ASyncedDelayShowsItsDivisions()
    {
        DelayEffect delay = (DelayEffect)EffectRules.Create(EffectKind.Delay, EffectId.From("e1"));

        RenderedMarkup free = await SettingsAsync(delay, false);
        RenderedMarkup synced = await SettingsAsync(delay with { Sync = true, Division = "1/8." }, false);

        Assert.DoesNotContain("1/8.", free.Texts);
        foreach (string division in EffectRules.Divisions)
        {
            Assert.Contains(division, synced.Texts);
        }
        Assert.Contains("true", synced.Values("data-chosen"));
        Assert.Contains("true", synced.Values("aria-pressed"));
    }

    /// <summary>A synced delay turns its time off with the reason; a project that is not live turns every setting off with its reason.</summary>
    [Fact]
    public void ATimeThatFollowsTheTempoIsTurnedOffAndSaysWhy()
    {
        DelayEffect delay = (DelayEffect)EffectRules.Create(EffectKind.Delay, EffectId.From("e1"));
        EffectParameter time = EffectRules.Find(delay, "time")!;
        EffectParameter feedback = EffectRules.Find(delay, "feedback")!;

        Assert.Null(EffectSettingRules.Block(delay, time, true, null));
        Assert.Equal(EffectSettingRules.FollowsTempo, EffectSettingRules.Block(delay with { Sync = true }, time, true, null));
        Assert.Null(EffectSettingRules.Block(delay with { Sync = true }, feedback, true, null));
        Assert.Equal(Offline, EffectSettingRules.Block(delay, feedback, false, Offline));
        Assert.Equal(Offline, EffectSettingRules.Block(delay with { Sync = true }, time, false, Offline));
    }

    /// <summary>When the project cannot be edited every knob says why, and so does the presets button.</summary>
    [Fact]
    public async Task WhenTheProjectCannotBeEditedEveryControlSaysWhy()
    {
        RenderedMarkup settings = await SettingsAsync(EffectRules.Create(EffectKind.Reverb, EffectId.From("e1")), false);

        Assert.Equal(EffectRules.Parameters(EffectKind.Reverb).Count + 1, settings.Values("title").Count(title => title == Offline));
    }
}
