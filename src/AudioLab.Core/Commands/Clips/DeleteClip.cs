using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes an audio or MIDI clip; in shuffle edit mode the later clips of its track slide left to close the gap.</summary>
public sealed record DeleteClip : Command
{
    /// <summary>The clip to remove.</summary>
    [Description("The clip to remove.")]
    public required ClipId Clip { get; init; }
}
