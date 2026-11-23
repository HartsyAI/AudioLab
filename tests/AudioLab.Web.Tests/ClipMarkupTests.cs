using AudioLab.Core.Model;
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

    /// <summary>A MIDI clip draws its real notes, so it claims no placeholder, and says how to open the piano roll.</summary>
    [Fact]
    public async Task AMidiClipClaimsNoPlaceholderAndSaysHowToOpenIt()
    {
        RenderedMarkup clip = await RenderedMarkup.OfAsync<Clip>(new Dictionary<string, object?>
        {
            ["Midi"] = Song.Midi("m1", 0, 4),
            ["ColorToken"] = "var(--track-1)",
        });

        Assert.DoesNotContain(clip.Values("aria-label"), label => label.Contains("placeholder", StringComparison.Ordinal));
        Assert.DoesNotContain(clip.Values("title"), title => title.Contains("placeholder", StringComparison.Ordinal));
        Assert.Contains(clip.Values("title"), title => title.Contains("piano roll", StringComparison.Ordinal));
    }

    /// <summary>However many notes a MIDI clip holds, they are one path in one svg and no element of their own.</summary>
    [Fact]
    public async Task TheNotesOfAMidiClipAreOnePathAndNoElementEach()
    {
        MidiNote[] notes = [.. Enumerable.Range(0, 500).Select(index => new MidiNote(new NoteId(index + 1), 48 + (index % 24), index * 0.01, 0.01, 90))];
        RenderedMarkup clip = await RenderedMarkup.OfAsync<Clip>(new Dictionary<string, object?>
        {
            ["Midi"] = Song.Midi("m1", 0, 8, notes),
            ["ColorToken"] = "var(--track-1)",
        });

        Assert.Single(clip.Values("d"));
        Assert.Equal(["500"], clip.Values("data-notes"));
        Assert.Equal(500, clip.Values("d")[0].Count(letter => letter == 'M'));
        Assert.DoesNotContain("note", clip.Values("class"));
    }
}
