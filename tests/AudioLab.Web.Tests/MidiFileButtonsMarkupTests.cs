using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Web.Components.PianoRoll;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The Import and Export buttons say why they are off until the seam is built; Export is a download link once it is.</summary>
public sealed class MidiFileButtonsMarkupTests
{
    private static Task<RenderedMarkup> RenderAsync<T>(IMidiTransfer transfer, params (string Name, object? Value)[] more)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        Dictionary<string, object?> parameters = new() { ["Project"] = ProjectId.From("song"), ["Clip"] = ClipId.From("clp_1") };
        foreach ((string name, object? value) in more)
        {
            parameters[name] = value;
        }
        return RenderedMarkup.OfAsync<T>(parameters, services => services.AddSingleton(transfer).AddSingleton<IElementClick, NoClick>());
    }

    /// <summary>With no implementation both buttons are off with the reason as their title, there is no file chooser, and no link.</summary>
    [Fact]
    public async Task WithNoImplementationBothButtonsAreOffWithTheReasonAndThereIsNoChooser()
    {
        UnavailableMidiTransfer transfer = new();

        RenderedMarkup buttons = await RenderAsync<MidiFileButtons>(transfer, ("CanEdit", true));

        string reason = transfer.Unavailable!;
        Assert.Equal([reason, reason], buttons.Values("title"));
        Assert.Equal(["true", "true"], buttons.Values("aria-disabled"));
        Assert.Empty(buttons.Values("href"));
        Assert.Empty(buttons.Values("accept"));
    }

    /// <summary>With an implementation Export is a download link to the route of the clip, and it is not disabled.</summary>
    [Fact]
    public async Task WithAnImplementationExportIsADownloadLinkToTheClip()
    {
        RenderedMarkup export = await RenderAsync<MidiExportButton>(new Available());

        Assert.Equal(["/api/projects/song/clips/clp_1/midi"], export.Values("href"));
        Assert.Contains("download", export.Attributes.Select(attribute => attribute.Name));
        Assert.DoesNotContain("true", export.Values("aria-disabled"));
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
}
