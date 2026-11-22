using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Removes every controller event of a MIDI clip, or only those of one controller.</summary>
public sealed record ClearControlChanges : Command
{
    /// <summary>The MIDI clip to clear.</summary>
    [Description("The MIDI clip to clear.")]
    public required ClipId Clip { get; init; }

    /// <summary>The controller number to clear, 0 to 127; every controller when absent.</summary>
    [Description("The controller number to clear, 0 to 127; every controller when absent.")]
    [Range(0, NoteRules.HighestPitch)]
    public int? Controller { get; init; }
}
