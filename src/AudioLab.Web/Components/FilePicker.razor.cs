using AudioLab.Web.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Components;

/// <summary>The browser's file chooser for audio, which a button opens and a drag of files lands on; the files go to the media library.</summary>
/// <remarks>
/// The browser reads a chosen file through the element it was chosen in, so the element stays in the page while any upload may still read
/// from it, and is replaced only when none is left, so that choosing the same file again raises a change.
/// </remarks>
public sealed partial class FilePicker : IDisposable
{
    /// <summary>The id of the one chooser of the session screen, which the Add audio button opens and a drop lands on.</summary>
    public const string ChooserId = "media-picker";

    private int _generation;

    private bool _used;

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

    /// <inheritdoc />
    public void Dispose()
    {
        Library.Changed -= OnLibraryChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Library.Changed += OnLibraryChanged;
    }

    private void OnLibraryChanged()
    {
        if (_used && Library.Uploads.Count == 0)
        {
            _used = false;
            _generation++;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private async Task ChosenAsync(InputFileChangeEventArgs chosen)
    {
        _used = true;
        Library.Add(chosen.GetMultipleFiles(chosen.FileCount).Select(file => new BrowserFileSource(file)));
        await OnChosen.InvokeAsync();
    }
}
