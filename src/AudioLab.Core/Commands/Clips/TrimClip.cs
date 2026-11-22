using System.ComponentModel;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves one edge of an audio clip, hiding or showing the file's audio without changing it.</summary>
public sealed record TrimClip : Command
{
    /// <summary>The audio clip to trim.</summary>
    [Description("The audio clip to trim.")]
    public required ClipId Clip { get; init; }

    /// <summary>start moves the left edge and keeps the audio after it in place; end moves the right edge.</summary>
    [Description("start moves the left edge and keeps the audio after it in place; end moves the right edge.")]
    public required ClipEdge Edge { get; init; }

    /// <summary>Where the edge goes on the timeline, in seconds; kept inside the file, off the start of the project and at least 0.05 seconds from the other edge.</summary>
    [Description("Where the edge goes on the timeline, in seconds; kept inside the file, off the start of the project and at least 0.05 seconds from the other edge.")]
    public required double Time { get; init; }
}
