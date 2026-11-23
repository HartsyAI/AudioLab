using AudioLab.Web.Components;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of what the segmented control tells assistive technology.</summary>
public sealed class SegmentedMarkupTests
{
    private static Task<RenderedMarkup> RenderAsync(bool transport, string? value) =>
        RenderedMarkup.OfAsync<Segmented>(new Dictionary<string, object?>
        {
            ["Label"] = "Transport",
            ["Transport"] = transport,
            ["Value"] = value,
            ["Wide"] = "play",
            ["Items"] = (IReadOnlyList<SegmentItem>)
            [
                new("start", "Go to start", IconName.ToStart),
                new("stop", "Stop", IconName.Stop),
                new("play", "Play", IconName.Pause),
            ],
        });

    /// <summary>In the transport only play is a toggle: it says whether it is pressed, and the other buttons say nothing of the kind.</summary>
    [Fact]
    public async Task InTheTransportOnlyPlayIsAToggleAndSaysWhetherItIsPressed()
    {
        RenderedMarkup playing = await RenderAsync(transport: true, value: "play");
        RenderedMarkup stopped = await RenderAsync(transport: true, value: null);

        // An item that is not a toggle writes the attribute with no value, which is to say it does not have it.
        Assert.Equal(["true"], playing.Values("aria-pressed").Where(value => value != string.Empty).Distinct());
        Assert.Equal(["false"], stopped.Values("aria-pressed").Where(value => value != string.Empty).Distinct());
        Assert.Contains(string.Empty, playing.Values("aria-pressed"));
    }

    /// <summary>A choice of a set says for each choice whether it is the chosen one.</summary>
    [Fact]
    public async Task AChoiceOfASetSaysForEachChoiceWhetherItIsTheChosenOne()
    {
        RenderedMarkup tool = await RenderAsync(transport: false, value: "stop");

        Assert.DoesNotContain(string.Empty, tool.Values("aria-pressed"));
        Assert.Contains("true", tool.Values("aria-pressed"));
        Assert.Contains("false", tool.Values("aria-pressed"));
    }
}
