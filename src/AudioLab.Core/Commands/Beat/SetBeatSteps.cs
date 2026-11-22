using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets how many steps one pass of the pattern has; every lane keeps its leading steps and new steps are off.</summary>
public sealed record SetBeatSteps : Command
{
    /// <summary>The step count, from 1 to 256; the old DAW offered 16 and 32.</summary>
    [Description("The step count, from 1 to 256; the old DAW offered 16 and 32.")]
    [Range(1, ProjectLimits.MaxBeatSteps)]
    public required int Steps { get; init; }
}
