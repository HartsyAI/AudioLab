using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Components.Lyrics;

/// <summary>The Import LRC button: the framework's <c>InputFile</c>, which the button opens, so no script is needed.</summary>
public sealed partial class LrcFileButton
{
    /// <summary>The id of the file chooser.</summary>
    public const string ChooserId = "lyrics-lrc-chooser";

    /// <summary>Whether the project can be edited, which an import does.</summary>
    [Parameter]
    public bool CanEdit { get; set; }

    /// <summary>Why the project cannot be edited, when it cannot.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Raised with the file the user chose.</summary>
    [Parameter]
    public EventCallback<InputFileChangeEventArgs> OnChosen { get; set; }

    [Inject]
    private IElementClick Click { get; set; } = default!;

    private void Choose() => Click.Click(ChooserId);
}
