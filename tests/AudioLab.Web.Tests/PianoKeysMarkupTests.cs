using AudioLab.Web.Components.PianoRoll;
using AudioLab.Web.Session.Roll;

namespace AudioLab.Web.Tests;

/// <summary>The keys say what they do and why they cannot be heard, to a reader and to assistive technology.</summary>
public sealed class PianoKeysMarkupTests
{
    private static Task<RenderedMarkup> RenderAsync(string? reason)
    {
        return RenderedMarkup.OfAsync<PianoKeys>(
            new Dictionary<string, object?> { ["Geometry"] = new RollGeometry(80, 12, 4), ["Reason"] = reason, ["Pitch"] = 64 });
    }

    /// <summary>A reason nothing can be heard is text the keys are described by, so a screen reader says it, and the keys are a tab stop.</summary>
    [Fact]
    public async Task TheReasonNothingCanBeHeardIsExposedToAssistiveTechnology()
    {
        RenderedMarkup keys = await RenderAsync("Notes cannot be heard yet.");

        Assert.Equal(["roll-keys-reason"], keys.Values("aria-describedby"));
        Assert.Contains("Notes cannot be heard yet.", keys.Texts);
        Assert.Equal(["Notes cannot be heard yet."], keys.Values("title"));
        Assert.Equal(["group"], keys.Values("role"));
        Assert.Equal(["0"], keys.Values("tabindex"));
        Assert.Equal(["false"], keys.Values("data-hearable"));
    }

    /// <summary>With sound the keys have no reason to give, and the pitch that has the cursor is marked on one key.</summary>
    [Fact]
    public async Task WithSoundThereIsNoReasonAndOneKeyHasTheCursor()
    {
        RenderedMarkup keys = await RenderAsync(null);

        Assert.Empty(keys.Values("aria-describedby"));
        Assert.Equal(["true"], keys.Values("data-hearable"));
        Assert.Equal(1, keys.Values("data-cursor").Count(value => value == "true"));
        Assert.Contains("Enter or Space plays it while held", Assert.Single(keys.Values("aria-label")), StringComparison.Ordinal);
    }
}
