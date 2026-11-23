using AudioLab.Web.Components;
using AudioLab.Web.Waveforms;

namespace AudioLab.Web.Tests;

/// <summary>What a clip says about itself to a reader and to assistive technology, in each state of its waveform.</summary>
public sealed class ClipMarkupTests
{
    private static Task<RenderedMarkup> AudioClip(ClipWave wave)
    {
        return RenderedMarkup.OfAsync<Clip>(new Dictionary<string, object?>
        {
            ["Audio"] = Song.Audio("a1", 0, 4),
            ["ColorToken"] = "var(--track-1)",
            ["Wave"] = wave,
        });
    }

    /// <summary>A clip whose peaks are drawn claims nothing about its waveform: it is the measurement.</summary>
    [Fact]
    public async Task AClipDrawnFromPeaksClaimsNothingAboutItsWaveform()
    {
        RenderedMarkup clip = await AudioClip(new ClipWave(WavePhase.Ready, "M0,50 Z"));

        Assert.Contains(clip.Values("aria-label"), label => label.EndsWith(", audio clip", StringComparison.Ordinal));
        Assert.Empty(clip.Values("title"));
        Assert.Contains("ready", clip.Values("data-peaks"));
        Assert.Contains("M0,50 Z", clip.Values("d"));
        Assert.DoesNotContain(clip.Values("aria-label"), label => label.Contains("placeholder", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A clip whose peaks are on their way says so and draws no path.</summary>
    [Fact]
    public async Task AClipWhosePeaksAreOnTheirWaySaysSoAndDrawsNoPath()
    {
        RenderedMarkup clip = await AudioClip(ClipWave.Loading);

        Assert.Contains(clip.Values("aria-label"), label => label.EndsWith("The waveform is loading.", StringComparison.Ordinal));
        Assert.Contains("The waveform is loading.", clip.Values("title"));
        Assert.Contains("loading", clip.Values("data-peaks"));
        Assert.Empty(clip.Values("d"));
    }

    /// <summary>A clip whose peaks failed gives the reason and how to try again, in its label and on its body.</summary>
    [Fact]
    public async Task AClipWhosePeaksFailedGivesTheReasonAndHowToTryAgain()
    {
        RenderedMarkup clip = await AudioClip(new ClipWave(WavePhase.Failed, null, "There is no such media file."));

        string expected = "The waveform could not be loaded: There is no such media file. Select the clip to try again.";
        Assert.Contains(clip.Values("aria-label"), label => label.EndsWith(expected, StringComparison.Ordinal));
        Assert.Contains(expected, clip.Values("title"));
        Assert.Contains("failed", clip.Values("data-peaks"));
    }

    /// <summary>A MIDI clip draws its real notes, so it says nothing about a waveform.</summary>
    [Fact]
    public async Task AMidiClipSaysNothingAboutAWaveform()
    {
        RenderedMarkup clip = await RenderedMarkup.OfAsync<Clip>(new Dictionary<string, object?>
        {
            ["Midi"] = Song.Midi("m1", 0, 4),
            ["ColorToken"] = "var(--track-1)",
        });

        Assert.DoesNotContain(clip.Values("aria-label"), label => label.Contains("waveform", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(clip.Values("title"));
    }
}
