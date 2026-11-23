using System.Net;
using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Waveforms;
using Microsoft.Extensions.DependencyInjection;
using static AudioLab.Web.Tests.PeakSupport;

namespace AudioLab.Web.Tests;

/// <summary>What a clip says about itself to a reader and to assistive technology, in each state of its waveform.</summary>
public sealed class ClipMarkupTests
{
    private static readonly AudioClip _audio = Song.Audio("a1", 0, 4) with { Media = MediaId.From("m1") };

    // A service whose file is in the state the test wants: loading forever, failed, or drawn.
    private static async Task<WaveformService> ServiceAsync(WavePhase phase)
    {
        byte[] file = PeakBytes(200_000);
        HttpMessageHandler handler = phase == WavePhase.Loading
            ? new NeverHandler()
            : new RecordingHandler(request => phase == WavePhase.Failed
                ? Refusal(HttpStatusCode.NotFound, "There is no such media file.")
                : Slice(request, file));
        WaveformService service = new(Store(handler));
        if (phase != WavePhase.Loading)
        {
            await SettledAsync(service, _audio, 136);
        }
        return service;
    }

    private static Task<RenderedMarkup> AudioClip(WaveformService service)
    {
        return RenderedMarkup.OfAsync<Clip>(
            new Dictionary<string, object?> { ["Audio"] = _audio, ["ColorToken"] = "var(--track-1)", ["Width"] = 136d },
            services => services.AddSingleton(service));
    }

    /// <summary>A clip whose peaks are drawn claims nothing about its waveform: it is the measurement.</summary>
    [Fact]
    public async Task AClipDrawnFromPeaksClaimsNothingAboutItsWaveform()
    {
        RenderedMarkup clip = await AudioClip(await ServiceAsync(WavePhase.Ready));

        Assert.Contains(clip.Values("aria-label"), label => label.EndsWith(", audio clip", StringComparison.Ordinal));
        Assert.Empty(clip.Values("title"));
        Assert.Contains("ready", clip.Values("data-peaks"));
        Assert.Single(clip.Values("d"), path => path.StartsWith("M0,50 L0", StringComparison.Ordinal));
        Assert.DoesNotContain(clip.Values("aria-label"), label => label.Contains("placeholder", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A clip whose peaks are on their way says so and draws no path.</summary>
    [Fact]
    public async Task AClipWhosePeaksAreOnTheirWaySaysSoAndDrawsNoPath()
    {
        RenderedMarkup clip = await AudioClip(await ServiceAsync(WavePhase.Loading));

        Assert.Contains(clip.Values("aria-label"), label => label.EndsWith("The waveform is loading.", StringComparison.Ordinal));
        Assert.Contains("The waveform is loading.", clip.Values("title"));
        Assert.Contains("loading", clip.Values("data-peaks"));
        Assert.Empty(clip.Values("d"));
    }

    /// <summary>A clip whose peaks failed gives the reason and how to try again, in its label and title.</summary>
    [Fact]
    public async Task AClipWhosePeaksFailedGivesTheReasonAndHowToTryAgain()
    {
        RenderedMarkup clip = await AudioClip(await ServiceAsync(WavePhase.Failed));

        string expected = "The waveform could not be loaded: There is no such media file. Select the clip to try again.";
        Assert.Contains(clip.Values("aria-label"), label => label.EndsWith(expected, StringComparison.Ordinal));
        Assert.Contains(expected, clip.Values("title"));
        Assert.Contains("failed", clip.Values("data-peaks"));
    }

    /// <summary>A clip that is selected while its waveform has failed asks for the peaks again, however it was selected.</summary>
    [Fact]
    public async Task AClipThatIsSelectedWhileItsWaveformHasFailedAsksAgain()
    {
        WaveformService service = await ServiceAsync(WavePhase.Failed);
        List<string> arrived = [];
        service.Arrived += arrived.Add;

        _ = await RenderedMarkup.OfAsync<Clip>(
            new Dictionary<string, object?> { ["Audio"] = _audio, ["ColorToken"] = "var(--track-1)", ["Width"] = 136d, ["Selected"] = true },
            services => services.AddSingleton(service));

        Assert.Contains("m1", arrived);
    }

    // A MIDI clip is drawn with a waveform service that has no file, since a clip injects one whatever it holds.
    private static Task<RenderedMarkup> RenderMidi(MidiClip clip)
    {
        return RenderedMarkup.OfAsync<Clip>(
            new Dictionary<string, object?> { ["Midi"] = clip, ["ColorToken"] = "var(--track-1)" },
            services => services.AddSingleton(new WaveformService(Store(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))))));
    }

    /// <summary>A MIDI clip draws its real notes, so it claims no placeholder, and says how to open the piano roll.</summary>
    [Fact]
    public async Task AMidiClipClaimsNoPlaceholderAndSaysHowToOpenIt()
    {
        RenderedMarkup clip = await RenderMidi(Song.Midi("m1", 0, 4));

        Assert.DoesNotContain(clip.Values("aria-label"), label => label.Contains("placeholder", StringComparison.Ordinal));
        Assert.DoesNotContain(clip.Values("title"), title => title.Contains("placeholder", StringComparison.Ordinal));
        Assert.Contains(clip.Values("title"), title => title.Contains("piano roll", StringComparison.Ordinal));
    }

    /// <summary>However many notes a MIDI clip holds, they are one path in one svg and no element of their own.</summary>
    [Fact]
    public async Task TheNotesOfAMidiClipAreOnePathAndNoElementEach()
    {
        MidiNote[] notes =
            [.. Enumerable.Range(0, 500).Select(index => new MidiNote(new NoteId(index + 1), 48 + (index % 24), index * 0.01, 0.01, 90))];

        RenderedMarkup clip = await RenderMidi(Song.Midi("m1", 0, 8, notes));

        Assert.Single(clip.Values("d"));
        Assert.Equal(["500"], clip.Values("data-notes"));
        Assert.Equal(500, clip.Values("d")[0].Count(letter => letter == 'M'));
        Assert.DoesNotContain("note", clip.Values("class"));
    }

    /// <summary>A MIDI clip says nothing about a waveform.</summary>
    [Fact]
    public async Task AMidiClipSaysNothingAboutAWaveform()
    {
        RenderedMarkup clip = await RenderedMarkup.OfAsync<Clip>(
            new Dictionary<string, object?> { ["Midi"] = Song.Midi("m1", 0, 4), ["ColorToken"] = "var(--track-1)" },
            services => services.AddSingleton(new WaveformService(Store(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))))));

        Assert.DoesNotContain(clip.Values("aria-label"), label => label.Contains("waveform", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(clip.Values("title"), title => title.Contains("waveform", StringComparison.OrdinalIgnoreCase));
    }
}
