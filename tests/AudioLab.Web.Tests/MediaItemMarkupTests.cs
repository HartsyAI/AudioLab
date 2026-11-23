using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Media;

namespace AudioLab.Web.Tests;

/// <summary>What a media row and a progress say to a reader and to assistive technology.</summary>
public sealed class MediaItemMarkupTests
{
    private static readonly MediaSummary _file = new(
        "m1", "kick.wav", MediaKind.Audio, MediaFormat.Wav, SampleEncoding.Pcm16, 1.5, 44100, 2, 264_644, new string('0', 64), DateTimeOffset.UnixEpoch);

    /// <summary>A file's row is named by everything it shows, and its delete control by the file and the shortcut.</summary>
    [Fact]
    public async Task AFilesRowIsNamedByEverythingItShowsAndItsDeleteByTheFileAndTheShortcut()
    {
        RenderedMarkup row = await RenderedMarkup.OfAsync<MediaItem>(new Dictionary<string, object?> { ["File"] = _file, ["TabStop"] = true });

        Assert.Contains("kick.wav, 0:01.5 long, 16-bit 44.1 kHz stereo, 258 KiB", row.Values("aria-label"));
        Assert.Contains("Delete kick.wav", row.Values("aria-label"));
        Assert.Contains("Delete kick.wav (Delete)", row.Values("title"));
        Assert.Contains("0", row.Values("tabindex"));
        Assert.Contains("-1", row.Values("tabindex"));
    }

    /// <summary>A refused upload says why, and offers to try again and to dismiss.</summary>
    [Fact]
    public async Task ARefusedUploadSaysWhyAndOffersToTryAgainAndToDismiss()
    {
        UploadJob job = Failed("kick.wav", "The file is cut short.");

        RenderedMarkup row = await RenderedMarkup.OfAsync<MediaItem>(new Dictionary<string, object?> { ["Job"] = job });

        Assert.Contains("The file is cut short.", row.Texts);
        Assert.Contains("Try again", row.Texts);
        Assert.Contains("Dismiss kick.wav", row.Values("aria-label"));
    }

    /// <summary>A waiting upload has a spinner and no percentage, and a reading one has a bar with its share.</summary>
    [Fact]
    public async Task AWaitingUploadHasASpinnerAndAReadingOneHasABarWithItsShare()
    {
        RenderedMarkup spinner = await RenderedMarkup.OfAsync<Progress>(new Dictionary<string, object?>
        {
            ["Label"] = "Uploading a.wav",
            ["Text"] = "Waiting for the server, 3 s",
        });
        RenderedMarkup bar = await RenderedMarkup.OfAsync<Progress>(new Dictionary<string, object?>
        {
            ["Label"] = "Uploading a.wav",
            ["Fraction"] = (double?)0.425,
            ["Text"] = "Reading 42%",
        });

        Assert.Empty(spinner.Values("aria-valuenow"));
        Assert.Empty(spinner.Values("style"));
        Assert.Contains("Waiting for the server, 3 s", spinner.Values("aria-valuetext"));
        Assert.Contains("42.5", bar.Values("aria-valuenow"));
        Assert.Contains("width: 42.5%", bar.Values("style"));
    }

    private static UploadJob Failed(string name, string reason)
    {
        return new UploadJob(1, new NamedSource(name)) { State = UploadState.Failed, Failure = reason };
    }

    private sealed class NamedSource(string name) : IUploadSource
    {
        public string Name => name;

        public long Length => 100;

        public Stream Open() => throw new NotSupportedException();
    }
}
