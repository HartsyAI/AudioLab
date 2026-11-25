using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Components.Lyrics;

/// <summary>The Import LRC button: the framework's <c>InputFile</c>, which the button opens, so no script is needed.</summary>
/// <remarks>A chooser is used once: after a pick the next one is a new element, so the same file can be picked again.</remarks>
public sealed partial class LrcFileButton
{
    /// <summary>The id of the file chooser.</summary>
    public const string ChooserId = "lyrics-lrc-chooser";

    private int _number;

    /// <summary>Whether the project can be edited, which an import does.</summary>
    [Parameter]
    public bool CanEdit { get; set; }

    /// <summary>Why the project cannot be edited, when it cannot.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised with the file the user chose; the chooser is replaced once this has finished reading it.</summary>
    [Parameter]
    public EventCallback<InputFileChangeEventArgs> OnChosen { get; set; }

    [Inject]
    private IElementClick Click { get; set; } = default!;

    private void Choose() => Click.Click(ChooserId);

    private async Task ChosenAsync(InputFileChangeEventArgs chosen)
    {
        await OnChosen.InvokeAsync(chosen);
        _number++;
    }
}
