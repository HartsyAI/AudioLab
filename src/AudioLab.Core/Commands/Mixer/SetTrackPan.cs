using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets a track's pan.</summary>
public sealed record SetTrackPan : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>From -1 (left) to 1 (right).</summary>
    [Description("From -1 (left) to 1 (right).")]
    [Range(-1, 1)]
    public required double Pan { get; init; }
}
