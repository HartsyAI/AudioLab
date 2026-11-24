using AudioLab.Web.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>A page may hold the open tab, as a MIDI clip opens the piano roll.</summary>
public sealed class TabsMarkupTests
{
    private static async Task<RenderedMarkup> RenderAsync(string? selected)
    {
        return await RenderedMarkup.OfAsync<Tabs>(
            new Dictionary<string, object?>
            {
                ["Names"] = new[] { "Mixer", "Piano roll" },
                ["Label"] = "Tools",
                ["Selected"] = selected,
            },
            services => services.AddSingleton<AudioLab.Web.Interop.IElementFocus, NoFocus>());
    }

    /// <summary>With nothing held the first tab is open; with a tab held, that one is.</summary>
    [Fact]
    public async Task WithNothingHeldTheFirstTabIsOpenAndWithATabHeldThatOneIs()
    {
        RenderedMarkup none = await RenderAsync(null);
        RenderedMarkup held = await RenderAsync("Piano roll");

        Assert.Equal(["true", "false"], none.Values("aria-selected"));
        Assert.Equal(["false", "true"], held.Values("aria-selected"));
    }

    private sealed class NoFocus : AudioLab.Web.Interop.IElementFocus
    {
        public void Focus(string elementId)
        {
        }

        public void FocusIn(string containerId, string selector)
        {
        }
    }
}
