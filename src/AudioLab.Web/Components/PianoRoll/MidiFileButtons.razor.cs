using AudioLab.Core.Model;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The Import and Export buttons of the roll, over the seam of <see cref="IMidiTransfer"/>; off, with a reason, until it is built.</summary>
/// <remarks>
/// Choosing a file is the framework's <c>InputFile</c>, which a button opens; the file's stream goes to the seam through the roll, so no script is
/// needed. Export is a link to the server's route, which sends the download headers.
/// </remarks>
public sealed partial class MidiFileButtons
{
    /// <summary>The id of the file chooser.</summary>
    public const string ChooserId = "roll-midi-chooser";

    /// <summary>The open project.</summary>
    [Parameter, EditorRequired]
    public ProjectId Project { get; set; }

    /// <summary>The MIDI clip that Export sends.</summary>
    [Parameter, EditorRequired]
    public ClipId Clip { get; set; }

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
    private IMidiTransfer Transfer { get; set; } = default!;

    [Inject]
    private IElementClick Click { get; set; } = default!;

    private void Choose() => Click.Click(ChooserId);
}
