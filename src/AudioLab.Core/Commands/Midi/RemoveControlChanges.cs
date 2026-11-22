using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Removes the events of one controller at one time from a MIDI clip.</summary>
public sealed record RemoveControlChanges : Command
{
    /// <summary>The MIDI clip the events belong to.</summary>
    [Description("The MIDI clip the events belong to.")]
    public required ClipId Clip { get; init; }

    /// <summary>Where the events are, in seconds from the start of the clip.</summary>
    [Description("Where the events are, in seconds from the start of the clip.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Time { get; init; }

    /// <summary>The controller number, 0 to 127.</summary>
    [Description("The controller number, 0 to 127.")]
    [Range(0, NoteRules.HighestPitch)]
    public required int Controller { get; init; }
}
