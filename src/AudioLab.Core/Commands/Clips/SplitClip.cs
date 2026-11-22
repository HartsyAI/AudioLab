using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Cuts an audio clip in two at a time inside it; the left piece keeps the clip and the server allocates the right piece's id.</summary>
public sealed record SplitClip : Command
{
    /// <summary>The audio clip to cut.</summary>
    [Description("The audio clip to cut.")]
    public required ClipId Clip { get; init; }

    /// <summary>Where to cut, in seconds on the timeline; more than 0.001 seconds inside the clip.</summary>
    [Description("Where to cut, in seconds on the timeline; more than 0.001 seconds inside the clip.")]
    public required double At { get; init; }
}
