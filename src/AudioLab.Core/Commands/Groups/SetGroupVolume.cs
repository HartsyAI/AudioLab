using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the gain multiplier applied to every member of a group.</summary>
public sealed record SetGroupVolume : Command
{
    /// <summary>The group to edit.</summary>
    [Description("The group to edit.")]
    public required GroupId Group { get; init; }

    /// <summary>The multiplier, from 0 to 16; 1 is unity.</summary>
    [Description("The multiplier, from 0 to 16; 1 is unity.")]
    [Range(0, ProjectLimits.MaxGain)]
    public required double Volume { get; init; }
}
