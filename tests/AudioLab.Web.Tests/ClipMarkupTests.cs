using AudioLab.Web.Components;

namespace AudioLab.Web.Tests;

/// <summary>What a clip says about itself to a reader and to assistive technology.</summary>
public sealed class ClipMarkupTests
{
    /// <summary>An audio clip's label and title say that its waveform is a placeholder.</summary>
    [Fact]
    public async Task AnAudioClipSaysItsWaveformIsAPlaceholder()
    {
        RenderedMarkup clip = await RenderedMarkup.OfAsync<Clip>(new Dictionary<string, object?>
        {
            ["Audio"] = Song.Audio("a1", 0, 4),
            ["ColorToken"] = "var(--track-1)",
            ["Waveform"] = "M0 0",
        });

        Assert.Contains(clip.Values("aria-label"), label => label.Contains("placeholder", StringComparison.Ordinal));
        Assert.Contains(clip.Values("title"), title => title.Contains("placeholder", StringComparison.Ordinal));
        Assert.Contains("placeholder", clip.Values("data-peaks"));
    }

    /// <summary>A MIDI clip draws its real notes, so it claims no placeholder.</summary>
    [Fact]
    public async Task AMidiClipClaimsNoPlaceholder()
    {
        RenderedMarkup clip = await RenderedMarkup.OfAsync<Clip>(new Dictionary<string, object?>
        {
            ["Midi"] = Song.Midi("m1", 0, 4),
            ["ColorToken"] = "var(--track-1)",
        });

        Assert.DoesNotContain(clip.Values("aria-label"), label => label.Contains("placeholder", StringComparison.Ordinal));
        Assert.Empty(clip.Values("title"));
    }
}
