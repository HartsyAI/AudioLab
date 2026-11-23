using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Web.Components.PianoRoll;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace AudioLab.Web.Tests;

/// <summary>The Import and Export buttons say why they are off until the seam is built, and are a file chooser and a link once it is.</summary>
public sealed class MidiFileButtonsMarkupTests
{
    private static Task<RenderedMarkup> RenderAsync(IMidiTransfer transfer)
    {
        return RenderedMarkup.OfAsync<MidiFileButtons>(
            new Dictionary<string, object?> { ["Project"] = ProjectId.From("song"), ["Clip"] = ClipId.From("clp_1"), ["CanEdit"] = true },
            services => services.AddSingleton(transfer).AddSingleton<IElementClick, NoClick>().AddSingleton<IJSRuntime, NoScript>());
    }

    /// <summary>With no implementation both buttons are off with the reason as their title, there is no file chooser, and no link.</summary>
    [Fact]
    public async Task WithNoImplementationBothButtonsAreOffWithTheReasonAndThereIsNoChooser()
    {
        UnavailableMidiTransfer transfer = new();

        RenderedMarkup buttons = await RenderAsync(transfer);

        string reason = transfer.Unavailable!;
        Assert.Equal([reason, reason], buttons.Values("title"));
        Assert.Equal(["true", "true"], buttons.Values("aria-disabled"));
        Assert.Empty(buttons.Values("href"));
        Assert.Empty(buttons.Values("accept"));
    }

    /// <summary>With an implementation the file chooser takes .mid files, and Export is a download link to the route of the clip.</summary>
    [Fact]
    public async Task WithAnImplementationThereIsAChooserForMidiFilesAndAnExportLinkToTheClip()
    {
        RenderedMarkup buttons = await RenderAsync(new Available());

        Assert.Contains(".mid", Assert.Single(buttons.Values("accept").Distinct()), StringComparison.Ordinal);
        Assert.Equal(["/api/projects/song/clips/clp_1/midi"], buttons.Values("href"));
        Assert.Contains("download", buttons.Attributes.Select(attribute => attribute.Name));
        Assert.DoesNotContain("true", buttons.Values("aria-disabled"));
    }

    private sealed class Available : IMidiTransfer
    {
        public string? Unavailable => null;

        public Task<MidiImportResult> ImportAsync(
            ProjectId project,
            TrackId track,
            string name,
            double start,
            Stream file,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Not used by this test.");
        }

        public Uri? ExportAddress(ProjectId project, ClipId clip) => new($"/api/projects/{project.Value}/clips/{clip.Value}/midi", UriKind.Relative);
    }

    private sealed class NoClick : IElementClick
    {
        public void Click(string elementId)
        {
        }
    }

    // The framework's file input asks the browser to hook itself up; nothing is there to answer in a test.
    private sealed class NoScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
