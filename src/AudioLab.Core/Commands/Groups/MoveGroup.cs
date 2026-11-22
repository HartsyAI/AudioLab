using System.ComponentModel;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves a whole group one step past the neighbouring track or group; at the end of the list nothing changes.</summary>
public sealed record MoveGroup : Command
{
    /// <summary>The group to move.</summary>
    [Description("The group to move.")]
    public required GroupId Group { get; init; }

    /// <summary>Up or down the track list.</summary>
    [Description("Up or down the track list.")]
    public required MoveDirection Direction { get; init; }
}
