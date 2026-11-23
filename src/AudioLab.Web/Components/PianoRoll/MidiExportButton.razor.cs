using AudioLab.Core.Model;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The Export button of the roll: a download link to the server's route for the clip, or off with the reason when there is none.</summary>
public sealed partial class MidiExportButton
{
    /// <summary>The open project.</summary>
    [Parameter, EditorRequired]
    public ProjectId Project { get; set; }

    /// <summary>The MIDI clip to send.</summary>
    [Parameter, EditorRequired]
    public ClipId Clip { get; set; }

    [Inject]
    private IMidiTransfer Transfer { get; set; } = default!;
}
