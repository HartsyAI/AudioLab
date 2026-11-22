using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the tempo from the start of the project until the first tempo change.</summary>
public sealed record SetTempoBpm : Command
{
    /// <summary>The tempo, in beats per minute, from 1 to 1000.</summary>
    [Description("The tempo, in beats per minute, from 1 to 1000.")]
    [Range(ProjectLimits.MinBpm, ProjectLimits.MaxBpm)]
    public required double Bpm { get; init; }
}
