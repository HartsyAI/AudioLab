using AudioLab.Web.Media;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Components;

/// <summary>The browser's file chooser for audio, which a button opens and a drag of files lands on; the files go to the media library.</summary>
/// <remarks>
/// The browser reads a chosen file through the element it was chosen in, and an element forgets its earlier selection when it is used
/// again. So each choice ends the use of its element: a new, empty one takes the id and the old one stays in the page, hidden, until the
/// uploads of its files are over. This also lets the same file be chosen twice in a row.
/// </remarks>
public sealed partial class FilePicker : IDisposable
{
    /// <summary>The id of the one chooser of the session screen, which the Add audio button opens and a drop lands on.</summary>
    public const string ChooserId = "media-picker";

    private readonly List<Chooser> _choosers = [new Chooser(0)];

    private int _next = 1;

    /// <summary>The element's id, so a button can open the chooser.</summary>
    [Parameter, EditorRequired]
    public string Id { get; set; } = string.Empty;

    /// <summary>A class from the component that places it; the picker styles nothing itself.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>What the chooser is called to assistive technology.</summary>
    [Parameter]
    public string Label { get; set; } = "Choose audio files to add";

    /// <summary>The chooser's place in the tab order; -1 for a chooser that a button opens, or one that only receives a drop.</summary>
    [Parameter]
    public int TabIndex { get; set; } = -1;

    /// <summary>Raised after the files were handed to the library, so a drop target can close.</summary>
    [Parameter]
    public EventCallback OnChosen { get; set; }

    [Inject]
    private MediaLibrary Library { get; set; } = default!;

    [Inject]
    private DecoderState Decoder { get; set; } = default!;

    // The compressed types are offered only while the decoder is installed, so the chooser never invites a file the server cannot read.
    private string AcceptTypes => MediaTypes.Accept(Decoder.Installed);

    /// <inheritdoc />
    public void Dispose()
    {
        Library.Changed -= OnLibraryChanged;
        Decoder.Changed -= OnDecoderChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Library.Changed += OnLibraryChanged;
        Decoder.Changed += OnDecoderChanged;
    }

    private void OnDecoderChanged() => _ = InvokeAsync(StateHasChanged);

    private string IdOf(Chooser chooser) => chooser == _choosers[^1] ? Id : $"{Id}-{chooser.Number}";

    private void OnLibraryChanged()
    {
        if (Prune())
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    // An old chooser goes when none of its files has an upload left, whether it finished or was dismissed.
    private bool Prune()
    {
        HashSet<long> live = [.. Library.Uploads.Select(job => job.Id)];
        return _choosers.RemoveAll(chooser => chooser != _choosers[^1] && !chooser.Jobs.Exists(live.Contains)) > 0;
    }

    private async Task ChosenAsync(InputFileChangeEventArgs chosen, Chooser chooser)
    {
        IReadOnlyList<UploadJob> jobs = Library.Add(chosen.GetMultipleFiles(chosen.FileCount).Select(file => new BrowserFileSource(file)));
        chooser.Jobs.AddRange(jobs.Select(job => job.Id));
        _choosers.Add(new Chooser(_next++));
        Prune();
        await OnChosen.InvokeAsync();
    }

    private sealed class Chooser(int number)
    {
        public int Number { get; } = number;

        public List<long> Jobs { get; } = [];
    }
}
